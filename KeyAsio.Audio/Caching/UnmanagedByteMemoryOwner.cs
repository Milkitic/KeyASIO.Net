using System.Buffers;
using System.Runtime.InteropServices;

namespace KeyAsio.Audio.Caching;

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
            return new MemoryHandle(_ptr + elementIndex);
        }

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

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

    internal byte* Pointer => (byte*)_ptr;

    public Memory<byte> Memory => _disposed || _manager == null ? Memory<byte>.Empty : _manager.Memory;

    public int Length => _length;

    public void Resize(int newLength)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UnmanagedByteMemoryOwner));
        if (newLength == _length) return;

        void* newPtr = NativeMemory.Realloc((void*)_ptr, (nuint)newLength);
        _ptr = (IntPtr)newPtr;
        _length = newLength;

        _manager = new UnmanagedByteMemoryManager((byte*)newPtr, newLength);
    }

    public static UnmanagedByteMemoryOwner Allocate(int length)
    {
        var p = NativeMemory.Alloc((nuint)length);
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
            NativeMemory.Free((void*)_ptr);
            _ptr = IntPtr.Zero;
        }

        if (disposing)
        {
            _length = 0;
            _manager = null;
        }
    }
}