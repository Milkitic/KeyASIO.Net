using System.Collections.Concurrent;
using System.Numerics.Tensors;
using KeyAsio.Core.Audio.Utils;
using NAudio.Utils;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders;

public sealed class QueueMixingSampleProvider : IMixingSampleProvider, IDisposable
{
    private readonly record struct PendingOperation(PendingOperationType Type, ISampleProvider Source);
    private enum PendingOperationType { Add, Remove }

    /// <summary>
    /// 一个特殊的返回值，表示该 Provider 虽然没有数据，但仍需保持在混合器中（不被移除）。
    /// </summary>
    public const int SignalKeepAlive = -0x1BF52;

    private const int MaxInputs = 1024;

    private readonly List<ISampleProvider> _sources = new(64);
    private readonly ConcurrentQueue<PendingOperation> _pendingOperations = new();

    private int _estimatedSourceCount;

    private bool _clearRequested;
    private float[] _mixerBuffer = new float[1024];
    private bool _isDisposed;

    public QueueMixingSampleProvider(WaveFormat waveFormat)
    {
        ArgumentNullException.ThrowIfNull(waveFormat);
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");
        WaveFormat = waveFormat;
    }

    public QueueMixingSampleProvider(IEnumerable<ISampleProvider> sources)
    {
        _sources = new List<ISampleProvider>();
        ISampleProvider? firstSource = null;
        foreach (var mixerInput in sources)
        {
            if (firstSource == null)
            {
                WaveFormat = mixerInput.WaveFormat
                             ?? throw new InvalidOperationException("Mixer input wave format is not initialized.");
                firstSource = mixerInput;
            }
            else
            {
                EnsureWaveFormat(mixerInput);
            }

            _sources.Add(mixerInput);
        }

        if (firstSource == null) throw new ArgumentException("Must provide at least one input");
        _estimatedSourceCount = _sources.Count;
    }

    public WaveFormat WaveFormat { get; private set; } = null!;

    public bool ReadFully { get; set; }
    public bool WantsKeep { get; set; }

    public void AddMixerInput(ISampleProvider mixerInput)
    {
        ArgumentNullException.ThrowIfNull(mixerInput);
        if (_isDisposed) return;

        EnsureWaveFormat(mixerInput);

        var currentCount = Interlocked.Increment(ref _estimatedSourceCount);
        if (currentCount > MaxInputs)
        {
            Interlocked.Decrement(ref _estimatedSourceCount);
            throw new InvalidOperationException("Too many mixer inputs");
        }

        _pendingOperations.Enqueue(new PendingOperation(PendingOperationType.Add, mixerInput));
    }

    public void RemoveMixerInput(ISampleProvider mixerInput)
    {
        _pendingOperations.Enqueue(new PendingOperation(PendingOperationType.Remove, mixerInput));
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
            if (WantsKeep) return SignalKeepAlive;

            if (ReadFully)
            {
                buffer.AsSpan(offset, count).Clear();
                return count;
            }

            return 0;
        }

        var maxSamplesRead = 0;

        _mixerBuffer = BufferHelpers.Ensure(_mixerBuffer, count);

        var outSpan = buffer.AsSpan(offset, count);
        outSpan.Clear();

        for (var index = _sources.Count - 1; index >= 0; index--)
        {
            var source = _sources[index];

            var samplesRead = source.Read(_mixerBuffer, 0, count);
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

            if (samplesRead == SignalKeepAlive) continue;

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

            // Drain all pending operations and discard additions (recycle them),
            // while discarding removals as well since the list is already cleared.
            while (_pendingOperations.TryDequeue(out var op))
            {
                if (op.Type == PendingOperationType.Add)
                {
                    AudioRecycling.QueueForRecycle(op.Source);
                }
            }

            Interlocked.Exchange(ref _estimatedSourceCount, 0);
            _clearRequested = false;
        }

        while (_pendingOperations.TryDequeue(out var op))
        {
            switch (op.Type)
            {
                case PendingOperationType.Add:
                    if (_sources.Count < MaxInputs)
                    {
                        _sources.Add(op.Source);
                    }
                    else
                    {
                        AudioRecycling.QueueForRecycle(op.Source);
                        Interlocked.Decrement(ref _estimatedSourceCount);
                    }

                    break;
                case PendingOperationType.Remove:
                    if (_sources.Remove(op.Source))
                    {
                        AudioRecycling.QueueForRecycle(op.Source);
                        Interlocked.Decrement(ref _estimatedSourceCount);
                    }

                    break;
            }
        }
    }

    private void EnsureWaveFormat(ISampleProvider mixerInput)
    {
        var mixerFormat = WaveFormat
                          ?? throw new InvalidOperationException("Mixer wave format is not initialized.");
        var inputFormat = mixerInput.WaveFormat
                          ?? throw new InvalidOperationException("Mixer input wave format is not initialized.");

        if (mixerFormat.SampleRate != inputFormat.SampleRate ||
            mixerFormat.Channels != inputFormat.Channels)
        {
            throw new ArgumentException("All mixer inputs must have the same WaveFormat");
        }
    }

    private void RemoveSourceAt(int index)
    {
        var lastIndex = _sources.Count - 1;
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
