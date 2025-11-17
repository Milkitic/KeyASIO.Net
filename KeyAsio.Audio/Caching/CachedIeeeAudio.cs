using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace KeyAsio.Audio.Caching;

public sealed class CachedIeeeAudio : IEquatable<CachedIeeeAudio>
{
    public readonly string SourceHash;
    public readonly float[] AudioData;
    public readonly WaveFormat WaveFormat;

    internal CachedIeeeAudio(string sourceHash, float[] audioData, WaveFormat waveFormat)
    {
        SourceHash = sourceHash;
        AudioData = audioData;
        WaveFormat = waveFormat;
    }

    public TimeSpan Duration => SamplesToTimeSpan(AudioData.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan SamplesToTimeSpan(int samples)
    {
        if (WaveFormat.Channels == 1)
            return TimeSpan.FromSeconds((samples) / (double)WaveFormat.SampleRate);
        if (WaveFormat.Channels == 2)
            return TimeSpan.FromSeconds((samples >> 1) / (double)WaveFormat.SampleRate);
        if (WaveFormat.Channels == 4)
            return TimeSpan.FromSeconds((samples >> 2) / (double)WaveFormat.SampleRate);
        if (WaveFormat.Channels == 8)
            return TimeSpan.FromSeconds((samples >> 3) / (double)WaveFormat.SampleRate);
        return TimeSpan.FromSeconds((samples / WaveFormat.Channels) / (double)WaveFormat.SampleRate);
    }

    public bool Equals(CachedIeeeAudio? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(SourceHash, other.SourceHash, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return SourceHash?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CachedIeeeAudio);
    }

    public static bool operator ==(CachedIeeeAudio? left, CachedIeeeAudio? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(CachedIeeeAudio? left, CachedIeeeAudio? right)
    {
        return !(left == right);
    }
}