using System.Buffers;
using System.Runtime.InteropServices;

namespace KeyAsio.Core.Audio.Caching;

internal sealed unsafe class UnmanagedByteMemoryOwner : IMemoryOwner<byte>
{
    private sealed class UnmanagedByteMemoryManager : MemoryManager<byte>
    {
        private readonly byte* _ptr;
        private readonly int _length;

        public UnmanagedByteMemoryManager(byte* ptr, int length)
        {
            _ptr = ptr;
            _length = length;
        }

        public override Span<byte> GetSpan() => new Span<byte>(_ptr, _length);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(_ptr + elementIndex);
        }

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private const int Alignment = 32;

    private IntPtr _ptr;
    private int _length;
    private UnmanagedByteMemoryManager? _manager;
    private bool _disposed;

    private UnmanagedByteMemoryOwner(IntPtr ptr, int length)
    {
        _ptr = ptr;
        _length = length;
        _manager = new UnmanagedByteMemoryManager((byte*)ptr, length);
    }

    /// <summary>
    /// 获取原生指针
    /// 注意：如果在调用 Resize 后，原本的指针会失效
    /// </summary>
    internal byte* Pointer => (byte*)_ptr;

    public Memory<byte> Memory => _disposed || _manager == null ? Memory<byte>.Empty : _manager.Memory;

    public int Length => _length;

    /// <summary>
    /// Must NOT resize after initialization.
    /// </summary>
    /// <param name="newLength"></param>
    /// <exception cref="ObjectDisposedException"></exception>
    public void Resize(int newLength)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UnmanagedByteMemoryOwner));
        if (newLength < 0) throw new ArgumentOutOfRangeException(nameof(newLength));
        if (newLength == _length) return;

        var size = (nuint)newLength;

        // 使用 AlignedRealloc 确保起始地址对齐
        var p = NativeMemory.AlignedRealloc((void*)_ptr, size, Alignment);

        _ptr = (IntPtr)p;
        _length = newLength;

        _manager = new UnmanagedByteMemoryManager((byte*)p, newLength);
    }

    public static UnmanagedByteMemoryOwner Allocate(int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        var size = (nuint)length;

        // 使用 AlignedAlloc 确保起始地址对齐
        var p = NativeMemory.AlignedAlloc(size, Alignment);

        return new UnmanagedByteMemoryOwner((IntPtr)p, length);
    }

    ~UnmanagedByteMemoryOwner()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (_ptr != IntPtr.Zero)
        {
            NativeMemory.AlignedFree((void*)_ptr);
            _ptr = IntPtr.Zero;
        }

        if (disposing)
        {
            _length = 0;
            _manager = null;
        }
    }
}