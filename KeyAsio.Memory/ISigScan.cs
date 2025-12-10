namespace KeyAsio.Memory;

public interface ISigScan
{
    nint FindPattern(string pattern, int offset = 0);
    void Reload();
    void ResetRegion();
    bool ReadMemory(nint address, Span<byte> buffer, int size, out int bytesRead);
}