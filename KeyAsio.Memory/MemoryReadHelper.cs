using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyAsio.Memory;

public static class MemoryReadHelper
{
    private const int PtrSize = 4; // osu! is a 32-bit process
    private static readonly byte[] Buffer = new byte[1024];

    public static string GetManagedString(SigScan sigScan, IntPtr stringRefAddress)
    {
        var stringPointer = GetPointer(sigScan, stringRefAddress);
        if (stringPointer == IntPtr.Zero) return string.Empty;

        var length = GetValue<int>(sigScan, stringPointer + 4);
        if (length == 0) return string.Empty;

        return GetString(sigScan, stringPointer + 8, length * 2);
    }

    private static string GetString(SigScan sigScan, IntPtr elementStartPointer, int bytesCount)
    {
        byte[]? buffer = null;
        var span = bytesCount < 256
            ? stackalloc byte[bytesCount]
            : buffer = ArrayPool<byte>.Shared.Rent(bytesCount);
        try
        {
            if (!sigScan.ReadMemory(elementStartPointer, span, bytesCount, out var bytesRead))
                throw new Exception("Failed to read memory.");
            if (bytesRead != bytesCount)
                throw new Exception("Failed to read complete string data.");
            return Encoding.Unicode.GetString(span.Slice(0, bytesCount));
        }
        finally
        {
            if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static T GetValue<T>(SigScan sigScan, IntPtr pointer) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (typeof(T) == typeof(bool)) size = 1; // Marshal.SizeOf<bool> is 4

        if (!sigScan.ReadMemory(pointer, Buffer, size, out _))
            throw new Exception("Failed to read memory.");

        var value = MemoryMarshal.Read<T>(Buffer);
        return value;
    }

    public static IntPtr GetPointer(SigScan sigScan, IntPtr parentPointer)
    {
        if (!sigScan.ReadMemory(parentPointer, Buffer, PtrSize, out _))
            throw new Exception("Failed to read memory.");

        var pointer = (nint)MemoryMarshal.Read<uint>(Buffer);
        return pointer;
    }
}