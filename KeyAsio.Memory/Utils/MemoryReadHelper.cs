using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyAsio.Memory.Utils;

public static class MemoryReadHelper
{
    private const int PtrSize = 4; // osu! is a 32-bit process

    public static bool TryGetManagedString(IMemoryReader memoryReader, IntPtr stringRefAddress, out string result)
    {
        result = string.Empty;
        if (!TryGetPointer(memoryReader, stringRefAddress, out var stringPointer)) return false;
        if (stringPointer == IntPtr.Zero) return true;

        if (!TryGetValue<int>(memoryReader, stringPointer + 4, out var length)) return false;
        if (length is > ushort.MaxValue or < 0) return false; // Sometimes got huge size
        if (length == 0) return true;

        return TryGetString(memoryReader, stringPointer + 8, length * 2, out result);
    }

    private static bool TryGetString(IMemoryReader memoryReader, IntPtr elementStartPointer, int bytesCount,
        out string result)
    {
        result = string.Empty;
        byte[]? buffer = null;
        var span = bytesCount < 128
            ? stackalloc byte[bytesCount]
            : (buffer = ArrayPool<byte>.Shared.Rent(bytesCount)).AsSpan(0, bytesCount);

        try
        {
            if (!memoryReader.ReadMemory(elementStartPointer, span, bytesCount, out var bytesRead) ||
                bytesRead != bytesCount)
            {
                return false;
            }

            result = Encoding.Unicode.GetString(span);
            return true;
        }
        finally
        {
            if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool TryGetValue<T>(IMemoryReader memoryReader, IntPtr pointer, out T result) where T : struct
    {
        result = default;
        int size = Unsafe.SizeOf<T>();
        if (typeof(T) == typeof(bool)) size = 1;

        byte[]? arrayPoolBuffer = null;
        Span<byte> buffer = size <= 128
            ? stackalloc byte[size]
            : (arrayPoolBuffer = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

        try
        {
            if (!memoryReader.ReadMemory(pointer, buffer, size, out _))
                return false;

            result = MemoryMarshal.Read<T>(buffer);
            return true;
        }
        finally
        {
            if (arrayPoolBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(arrayPoolBuffer);
            }
        }
    }

    public static bool TryGetPointer(IMemoryReader memoryReader, IntPtr parentPointer, out IntPtr result)
    {
        result = IntPtr.Zero;
        Span<byte> buffer = stackalloc byte[PtrSize];

        if (!memoryReader.ReadMemory(parentPointer, buffer, PtrSize, out _))
            return false;

        result = (nint)MemoryMarshal.Read<uint>(buffer);
        return true;
    }

    public static string GetManagedString(IMemoryReader memoryReader, IntPtr stringRefAddress)
    {
        return TryGetManagedString(memoryReader, stringRefAddress, out var res) ? res : string.Empty;
    }

    public static T GetValue<T>(IMemoryReader memoryReader, IntPtr pointer) where T : struct
    {
        return TryGetValue<T>(memoryReader, pointer, out var res) ? res : default;
    }

    public static IntPtr GetPointer(IMemoryReader memoryReader, IntPtr parentPointer)
    {
        return TryGetPointer(memoryReader, parentPointer, out var res) ? res : IntPtr.Zero;
    }
}