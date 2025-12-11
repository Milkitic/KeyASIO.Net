namespace KeyAsio.Memory;

public interface ISigScan
{
    IntPtr FindPattern(string pattern, int offset = 0);
    void Reload();
    void ResetRegion();
}