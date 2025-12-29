using System.Numerics.Tensors;
using KeyAsio.Core.Audio.Utils;
using NAudio.Utils;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders;

/// <summary>
/// A sample provider mixer, allowing inputs to be added and removed
/// </summary>
public sealed class EnhancedMixingSampleProvider : IMixingSampleProvider
{
    private readonly Lock _sourcesLock = new();
    private readonly List<ISampleProvider> _sources;
    private float[]? _sourceBuffer;
    private const int MaxInputs = 1024;

    /// <summary>
    /// Creates a new MixingSampleProvider, with no inputs, but a specified WaveFormat
    /// </summary>
    /// <param name="waveFormat">The WaveFormat of this mixer. All inputs must be in this format</param>
    public EnhancedMixingSampleProvider(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            throw new ArgumentException("Mixer wave format must be IEEE float");
        }

        _sources = new List<ISampleProvider>(64);
        WaveFormat = waveFormat;
    }

    /// <summary>
    /// Creates a new MixingSampleProvider, based on the given inputs
    /// </summary>
    /// <param name="sources">Mixer inputs - must all have the same waveformat, and must
    /// all be of the same WaveFormat. There must be at least one input</param>
    public EnhancedMixingSampleProvider(IEnumerable<ISampleProvider> sources)
    {
        _sources = new List<ISampleProvider>(64);
        foreach (var source in sources)
        {
            AddMixerInput(source);
        }

        if (_sources.Count == 0)
        {
            throw new ArgumentException("Must provide at least one input in this constructor");
        }
    }

    /// <summary>
    /// Returns the mixer inputs (read-only - use AddMixerInput to add an input
    /// </summary>
    public IEnumerable<ISampleProvider> MixerInputs => _sources;

    /// <summary>
    /// When set to true, the Read method always returns the number
    /// of samples requested, even if there are no inputs, or if the
    /// current inputs reach their end. Setting this to true effectively
    /// makes this a never-ending sample provider, so take care if you plan
    /// to write it out to a file.
    /// </summary>
    public bool ReadFully { get; set; }

    public bool WantsKeep { get; set; }

    /// <summary>
    /// Adds a new mixer input
    /// </summary>
    /// <param name="mixerInput">Mixer input</param>
    public void AddMixerInput(ISampleProvider mixerInput)
    {
        // we'll just call the lock around add since we are protecting against an AddMixerInput at
        // the same time as a Read, rather than two AddMixerInput calls at the same time
        lock (_sourcesLock)
        {
            if (_sources.Count >= MaxInputs)
            {
                throw new InvalidOperationException("Too many mixer inputs");
            }

            _sources.Add(mixerInput);
        }

        if (WaveFormat == null)
        {
            WaveFormat = mixerInput.WaveFormat;
        }
        else
        {
            if (WaveFormat.SampleRate != mixerInput.WaveFormat.SampleRate ||
                WaveFormat.Channels != mixerInput.WaveFormat.Channels)
            {
                throw new ArgumentException("All mixer inputs must have the same WaveFormat");
            }
        }
    }

    /// <summary>
    /// Removes a mixer input
    /// </summary>
    /// <param name="mixerInput">Mixer input to remove</param>
    public void RemoveMixerInput(ISampleProvider mixerInput)
    {
        lock (_sourcesLock)
        {
            if (_sources.Remove(mixerInput))
            {
                AudioRecycling.QueueForRecycle(mixerInput);
            }
        }
    }

    /// <summary>
    /// Removes all mixer inputs
    /// </summary>
    public void RemoveAllMixerInputs()
    {
        lock (_sourcesLock)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                _sources.RemoveAt(i);
                AudioRecycling.QueueForRecycle(source);
            }
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
    }

    /// <summary>
    /// The output WaveFormat of this sample provider
    /// </summary>
    public WaveFormat? WaveFormat { get; private set; }

    /// <summary>
    /// Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="count">Number of samples required</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        var outputSamples = 0;
        _sourceBuffer = BufferHelpers.Ensure(_sourceBuffer, count);
        var sourceBufferSpan = _sourceBuffer.AsSpan(0, count);
        var outputBufferSpan = buffer.AsSpan(offset, count);

        lock (_sourcesLock)
        {
            var index = _sources.Count - 1;
            while (index >= 0)
            {
                var source = _sources[index];
                var samplesRead = source.Read(_sourceBuffer, 0, count);

                var samplesToAdd = Math.Min(samplesRead, outputSamples);
                var samplesToCopy = samplesRead - samplesToAdd;

                if (samplesToAdd > 0)
                {
                    TensorPrimitives.Add(
                        outputBufferSpan.Slice(0, samplesToAdd),
                        sourceBufferSpan.Slice(0, samplesToAdd),
                        outputBufferSpan.Slice(0, samplesToAdd));
                }

                if (samplesToCopy > 0)
                {
                    sourceBufferSpan.Slice(samplesToAdd, samplesToCopy)
                        .CopyTo(outputBufferSpan.Slice(samplesToAdd, samplesToCopy));
                }

                outputSamples = Math.Max(samplesRead, outputSamples);
                if (samplesRead < count)
                {
                    RemoveSourceAt(index);
                    AudioRecycling.QueueForRecycle(source);
                }

                index--;
            }
        }

        // optionally ensure we return a full buffer
        if (ReadFully && outputSamples < count)
        {
            Array.Clear(buffer, offset + outputSamples, count - outputSamples);
            outputSamples = count;
        }

        return outputSamples;
    }
}