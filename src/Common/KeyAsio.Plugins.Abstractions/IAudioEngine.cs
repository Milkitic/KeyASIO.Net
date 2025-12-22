using NAudio.Wave;

namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Audio engine abstraction interface
/// </summary>
public interface IAudioEngine
{
    /// <summary>
    /// Gets the main mixer
    /// </summary>
    ISampleProvider MainMixer { get; }

    /// <summary>
    /// Gets the effect mixer
    /// </summary>
    ISampleProvider EffectMixer { get; }

    /// <summary>
    /// Gets the music mixer
    /// </summary>
    ISampleProvider MusicMixer { get; }

    /// <summary>
    /// Gets the engine's WaveFormat
    /// </summary>
    WaveFormat EngineWaveFormat { get; }

    /// <summary>
    /// Adds a mixer input to the main mixer
    /// </summary>
    void AddMixerInput(ISampleProvider input);

    /// <summary>
    /// Removes a mixer input from the main mixer
    /// </summary>
    void RemoveMixerInput(ISampleProvider input);
}
