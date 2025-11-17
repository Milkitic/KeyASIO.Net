using NAudio.Wave;

namespace KeyAsio.Audio.Caching;

public sealed class CachedAudio : IEquatable<CachedAudio>
{
    public readonly string SourceHash;
    public readonly byte[] AudioData;
    public readonly WaveFormat WaveFormat;

    internal CachedAudio(string sourceHash, byte[] audioData, WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm)
            throw new ArgumentException("Only PCM wave format is supported.", nameof(waveFormat));
        SourceHash = sourceHash;
        AudioData = audioData;
        WaveFormat = waveFormat;
    }

    public TimeSpan Duration => TimeSpan.FromSeconds((double)AudioData.Length / WaveFormat.AverageBytesPerSecond);

    public bool Equals(CachedAudio? other)
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
        return Equals(obj as CachedAudio);
    }

    public static bool operator ==(CachedAudio? left, CachedAudio? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(CachedAudio? left, CachedAudio? right)
    {
        return !(left == right);
    }
}