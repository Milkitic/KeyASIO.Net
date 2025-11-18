using System.Buffers;
using NAudio.Wave;

namespace KeyAsio.Audio.Caching;

public sealed class CachedAudio : IEquatable<CachedAudio>, IDisposable
{
    public readonly string SourceHash;
    public readonly WaveFormat WaveFormat;

    private IMemoryOwner<byte>? _owner;
    private int _length;
    private bool _disposed;

    internal CachedAudio(string sourceHash, IMemoryOwner<byte> owner, int length, WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm)
            throw new ArgumentException("Only PCM wave format is supported.", nameof(waveFormat));
        _owner = owner;
        _length = length;
        SourceHash = sourceHash;
        WaveFormat = waveFormat;
    }

    public TimeSpan Duration => TimeSpan.FromSeconds((double)_length / WaveFormat.AverageBytesPerSecond);
    public Span<byte> Span => _owner is null ? Span<byte>.Empty : _owner.Memory.Span.Slice(0, _length);
    public int Length => _length;

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
        _owner?.Dispose();
        _owner = null;
        _length = 0;
    }

    public bool IsDisposed => _disposed;
}