using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyAsio.Memory;

public static class MemoryReadHelper
{
    private const int PtrSize = 4; // osu! is a 32-bit process
    private static readonly byte[] Buffer = new byte[1024];

    public static string GetManagedString(IMemoryReader memoryReader, IntPtr stringRefAddress)
    {
        var stringPointer = GetPointer(memoryReader, stringRefAddress);
        if (stringPointer == IntPtr.Zero) return string.Empty;

        var length = GetValue<int>(memoryReader, stringPointer + 4);
        if (length == 0) return string.Empty;

        return GetString(memoryReader, stringPointer + 8, length * 2);
    }

    private static string GetString(IMemoryReader memoryReader, IntPtr elementStartPointer, int bytesCount)
    {
        byte[]? buffer = null;
        var span = bytesCount < 256
            ? stackalloc byte[bytesCount]
            : buffer = ArrayPool<byte>.Shared.Rent(bytesCount);
        try
        {
            if (!memoryReader.ReadMemory(elementStartPointer, span, bytesCount, out var bytesRead))
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

    public static T GetValue<T>(IMemoryReader memoryReader, IntPtr pointer) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (typeof(T) == typeof(bool)) size = 1; // Marshal.SizeOf<bool> is 4

        if (!memoryReader.ReadMemory(pointer, Buffer, size, out _))
            throw new Exception("Failed to read memory.");

        var value = MemoryMarshal.Read<T>(Buffer);
        return value;
    }

    public static IntPtr GetPointer(IMemoryReader memoryReader, IntPtr parentPointer)
    {
        if (!memoryReader.ReadMemory(parentPointer, Buffer, PtrSize, out _))
            throw new Exception("Failed to read memory.");

        var pointer = (nint)MemoryMarshal.Read<uint>(Buffer);
        return pointer;
    }
}