using System.Buffers;
using System.Numerics.Tensors;
using KeyAsio.Audio.SampleProviders.BalancePans;
using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public sealed class EnhancedMixingSampleProvider : ISampleProvider
{
    private readonly Lock _lock = new();
    private readonly List<ISampleProvider> _sources = new();
    private const int MaxInputs = 1024;

    public WaveFormat? WaveFormat { get; private set; }
    public bool ReadFully { get; set; }

    public EnhancedMixingSampleProvider(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");
        WaveFormat = waveFormat;
    }

    public EnhancedMixingSampleProvider(IEnumerable<ISampleProvider> sources)
    {
        _sources = new List<ISampleProvider>();
        foreach (var source in sources) AddMixerInput(source);
        if (_sources.Count == 0) throw new ArgumentException("Must provide at least one input");
    }

    public void AddMixerInput(ISampleProvider mixerInput)
    {
        lock (_lock)
        {
            if (_sources.Count >= MaxInputs)
                throw new InvalidOperationException("Too many mixer inputs");
            _sources.Add(mixerInput);
        }

        if (WaveFormat == null) WaveFormat = mixerInput.WaveFormat;
        else if (WaveFormat.SampleRate != mixerInput.WaveFormat.SampleRate ||
                 WaveFormat.Channels != mixerInput.WaveFormat.Channels)
        {
            throw new ArgumentException("All mixer inputs must have the same WaveFormat");
        }
    }

    public void RemoveMixerInput(ISampleProvider mixerInput)
    {
        lock (_lock)
        {
            var success = _sources.Remove(mixerInput);
            if (success)
            {
                RecycleSourceChain(mixerInput);
            }
        }
    }

    public void RemoveAllMixerInputs()
    {
        lock (_lock)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                _sources.RemoveAt(i);
                RecycleSourceChain(source);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int maxSamplesRead = 0;
        float[] mixerBuffer = ArrayPool<float>.Shared.Rent(count);
        try
        {
            var outSpan = buffer.AsSpan(offset, count);
            outSpan.Clear();

            lock (_lock)
            {
                int index = _sources.Count - 1;
                while (index >= 0)
                {
                    var source = _sources[index];

                    int samplesRead = source.Read(mixerBuffer, 0, count);
                    if (samplesRead > 0)
                    {
                        var srcSpan = mixerBuffer.AsSpan(0, samplesRead);
                        var dstSpan = outSpan.Slice(0, samplesRead);

                        TensorPrimitives.Add(dstSpan, srcSpan, dstSpan);
                        maxSamplesRead = Math.Max(maxSamplesRead, samplesRead);
                    }

                    if (samplesRead < count)
                    {
                        _sources.RemoveAt(index);
                        RecycleSourceChain(source);
                    }

                    index--;
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(mixerBuffer);
        }

        return ReadFully ? count : maxSamplesRead;
    }

    public IEnumerable<ISampleProvider> MixerInputs
    {
        get
        {
            lock (_lock) return _sources;
        }
    }

    private void RecycleSourceChain(ISampleProvider provider)
    {
        var current = provider;

        while (current is IRecyclableProvider recyclable)
        {
            var next = recyclable.ResetAndGetSource();
            switch (current)
            {
                case EnhancedVolumeSampleProvider vol:
                    RecyclableSampleProviderFactory.Return(vol);
                    break;
                case ProfessionalBalanceProvider pan:
                    RecyclableSampleProviderFactory.Return(pan);
                    break;
                case LoopSampleProvider loop:
                    RecyclableSampleProviderFactory.Return(loop);
                    break;
                case SeekableCachedAudioProvider cache:
                    RecyclableSampleProviderFactory.Return(cache);
                    break;
            }

            current = next;
        }
    }
}