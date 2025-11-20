using System.Collections.Concurrent;
using System.Numerics.Tensors;
using KeyAsio.Audio.Utils;
using NAudio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public sealed class EnhancedMixingSampleProvider : ISampleProvider, IDisposable
{
    private const int MaxInputs = 1024;

    private readonly List<ISampleProvider> _sources = new(64);
    private readonly ConcurrentQueue<ISampleProvider> _pendingAdditions = new();
    private readonly ConcurrentQueue<ISampleProvider> _pendingRemovals = new();

    private int _estimatedSourceCount;

    private bool _clearRequested;
    private float[] _mixerBuffer = new float[1024];
    private bool _isDisposed;

    public EnhancedMixingSampleProvider(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");
        WaveFormat = waveFormat;
    }

    public EnhancedMixingSampleProvider(IEnumerable<ISampleProvider> sources)
    {
        _sources = new List<ISampleProvider>();
        ISampleProvider? firstSource = null;
        foreach (var mixerInput in sources)
        {
            AddMixerInput(mixerInput);
            if (firstSource == null)
            {
                WaveFormat = mixerInput.WaveFormat;
                firstSource = mixerInput;
            }
            else
            {
                EnsureWaveFormat(mixerInput);
            }
        }

        if (firstSource == null) throw new ArgumentException("Must provide at least one input");
    }

    public WaveFormat? WaveFormat { get; }

    public bool ReadFully { get; set; }

    public void AddMixerInput(ISampleProvider mixerInput)
    {
        if (_isDisposed) return;

        EnsureWaveFormat(mixerInput);

        int currentCount = Interlocked.Increment(ref _estimatedSourceCount);
        if (currentCount > MaxInputs)
        {
            Interlocked.Decrement(ref _estimatedSourceCount);
            throw new InvalidOperationException("Too many mixer inputs");
        }

        _pendingAdditions.Enqueue(mixerInput);
    }

    public void RemoveMixerInput(ISampleProvider mixerInput)
    {
        _pendingRemovals.Enqueue(mixerInput);
    }

    public void RemoveAllMixerInputs()
    {
        _clearRequested = true;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        ProcessPendingChanges();

        if (_sources.Count == 0)
        {
            if (ReadFully)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            return 0;
        }

        int maxSamplesRead = 0;

        _mixerBuffer = BufferHelpers.Ensure(_mixerBuffer, count);

        var outSpan = buffer.AsSpan(offset, count);
        outSpan.Clear();

        for (int index = _sources.Count - 1; index >= 0; index--)
        {
            var source = _sources[index];

            int samplesRead = source.Read(_mixerBuffer, 0, count);
            if (samplesRead > 0)
            {
                var srcSpan = _mixerBuffer.AsSpan(0, samplesRead);
                var dstSpan = outSpan.Slice(0, samplesRead);

                TensorPrimitives.Add(dstSpan, srcSpan, dstSpan);

                if (samplesRead > maxSamplesRead)
                {
                    maxSamplesRead = samplesRead;
                }
            }

            if (samplesRead < count)
            {
                RemoveSourceAt(index);
                AudioRecycling.QueueForRecycle(source);
            }
        }

        return ReadFully ? count : maxSamplesRead;
    }

    private void ProcessPendingChanges()
    {
        if (_clearRequested)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                _sources.RemoveAt(i);
                AudioRecycling.QueueForRecycle(source);
            }

            while (_pendingAdditions.TryDequeue(out var pending))
            {
                AudioRecycling.QueueForRecycle(pending);
            }

            Interlocked.Exchange(ref _estimatedSourceCount, 0);
            _clearRequested = false;
        }

        while (_pendingRemovals.TryDequeue(out var toRemove))
        {
            if (_sources.Remove(toRemove))
            {
                AudioRecycling.QueueForRecycle(toRemove);
            }
        }

        while (_pendingAdditions.TryDequeue(out var source))
        {
            if (_sources.Count < MaxInputs)
            {
                _sources.Add(source);
            }
            else
            {
                // 理论上 AddMixerInput 已经拦截了，但这作为防御性编程
                AudioRecycling.QueueForRecycle(source);
                Interlocked.Decrement(ref _estimatedSourceCount);
            }
        }
    }

    private void EnsureWaveFormat(ISampleProvider mixerInput)
    {
        if (WaveFormat.SampleRate != mixerInput.WaveFormat.SampleRate ||
            WaveFormat.Channels != mixerInput.WaveFormat.Channels)
        {
            throw new ArgumentException("All mixer inputs must have the same WaveFormat");
        }
    }

    private void RemoveSourceAt(int index)
    {
        int lastIndex = _sources.Count - 1;
        if (index < lastIndex)
        {
            _sources[index] = _sources[lastIndex];
        }

        _sources.RemoveAt(lastIndex);
        Interlocked.Decrement(ref _estimatedSourceCount);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _clearRequested = true;
    }
}