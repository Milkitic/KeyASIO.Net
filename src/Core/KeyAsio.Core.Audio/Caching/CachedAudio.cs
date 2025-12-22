using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.Caching;

public sealed unsafe class CachedAudio : IEquatable<CachedAudio>, IDisposable
{
    public readonly string SourceHash;
    public readonly WaveFormat WaveFormat;
    private readonly bool _needSlice;

    private IMemoryOwner<byte>? _owner;
    private byte* _cachedPointer;
    private int _length;

    private volatile bool _isDisposingOrDisposed;
    private int _activeReaders;
    private int _activeSpanReaders;

    internal CachedAudio(string sourceHash, IMemoryOwner<byte> owner, int length, WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm)
            throw new ArgumentException("Only PCM wave format is supported.", nameof(waveFormat));

        _owner = owner;
        _length = length;
        _needSlice = _owner.Memory.Span.Length != _length;
        Debug.Assert(!_needSlice);
        SourceHash = sourceHash;
        WaveFormat = waveFormat;

        _cachedPointer = owner is UnmanagedByteMemoryOwner unmanaged ? unmanaged.Pointer : null;
    }

    public TimeSpan Duration => TimeSpan.FromSeconds((double)_length / WaveFormat.AverageBytesPerSecond);

    public int Length => _length;

    public bool IsDisposingOrDisposed => _isDisposingOrDisposed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAcquirePointer(out byte* ptr)
    {
        Interlocked.Increment(ref _activeReaders);

        if (_isDisposingOrDisposed)
        {
            Interlocked.Decrement(ref _activeReaders);
            ptr = null;
            return false;
        }

        ptr = _cachedPointer;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleasePointer()
    {
        Interlocked.Decrement(ref _activeReaders);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAcquireSpan(out Span<byte> span)
    {
        Interlocked.Increment(ref _activeSpanReaders);

        if (_isDisposingOrDisposed)
        {
            Interlocked.Decrement(ref _activeSpanReaders);
            span = null;
            return false;
        }

        if (_owner is null)
            span = Span<byte>.Empty;
        else if (_needSlice)
            span = _owner.Memory.Span.Slice(0, _length);
        else
            span = _owner.Memory.Span;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseSpan()
    {
        Interlocked.Decrement(ref _activeSpanReaders);
    }

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
        if (_isDisposingOrDisposed) return;
        _isDisposingOrDisposed = true;

        var spin = new SpinWait();
        while (_activeReaders > 0 || _activeSpanReaders > 0)
        {
            spin.SpinOnce();
        }

        _owner?.Dispose();
        _owner = null;
        _cachedPointer = null;
        _length = 0;
    }
}