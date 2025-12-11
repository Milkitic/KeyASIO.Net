namespace KeyAsio.Memory;

public interface IMemoryReader
{
    bool ReadMemory(nint address, Span<byte> buffer, int size, out int bytesRead);
}