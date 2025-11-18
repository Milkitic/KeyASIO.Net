using NAudio.Wave;

namespace KeyAsio.Audio.Caching;

public sealed class CachedAudio : IEquatable<CachedAudio>, IDisposable
{
    public readonly string SourceHash;
    public readonly WaveFormat WaveFormat;

    private byte[]? _audioData;
    private bool _disposed;

    internal CachedAudio(string sourceHash, byte[] audioData, WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm)
            throw new ArgumentException("Only PCM wave format is supported.", nameof(waveFormat));
        _audioData = audioData;

        SourceHash = sourceHash;
        WaveFormat = waveFormat;
    }

    public TimeSpan Duration => TimeSpan.FromSeconds((double)(
        _audioData?.Length ?? 0) / WaveFormat.AverageBytesPerSecond);
    public Span<byte> Span => _audioData is { Length: > 0 } ? _audioData : Span<byte>.Empty;

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _audioData = null;
    }

    public bool IsDisposed => _disposed;
}