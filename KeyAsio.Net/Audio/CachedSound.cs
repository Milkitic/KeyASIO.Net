using NAudio.Wave;

namespace KeyAsio.Net.Audio;

public class CachedSound
{
    public string SourcePath { get; }
    public ReadOnlyMemory<float> AudioData { get; }
    public WaveFormat WaveFormat { get; }
    public TimeSpan Duration { get; }

    internal CachedSound(string filePath, ReadOnlyMemory<float> audioData, TimeSpan duration, WaveFormat waveFormat)
    {
        SourcePath = filePath;
        AudioData = audioData;
        Duration = duration;
        WaveFormat = waveFormat;
    }

    public override bool Equals(object? obj)
    {
        if (obj is CachedSound other)
            return Equals(other);
        return ReferenceEquals(this, obj);
    }

    protected bool Equals(CachedSound other)
    {
        return SourcePath == other.SourcePath;
    }

    public override int GetHashCode()
    {
        return SourcePath.GetHashCode();
    }
}