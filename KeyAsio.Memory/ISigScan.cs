namespace KeyAsio.Memory;

public interface ISigScan
{
    nint FindPattern(string pattern, int offset = 0);
    void Reload();
    void ResetRegion();
}