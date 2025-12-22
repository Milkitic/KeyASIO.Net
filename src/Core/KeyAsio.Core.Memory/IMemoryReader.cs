namespace KeyAsio.Core.Memory;

public interface IMemoryReader
{
    bool ReadMemory(IntPtr address, Span<byte> buffer, int size, out int bytesRead);
}