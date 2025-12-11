namespace KeyAsio.Memory;

public class CachedStringReader
{
    private IntPtr _lastPtr = IntPtr.Zero;
    private string _cachedValue = string.Empty;

    public string Get(IMemoryReader memoryReader, IntPtr stringRefAddr)
    {
        var currentPtr = MemoryReadHelper.GetPointer(memoryReader, stringRefAddr);

        if (currentPtr == _lastPtr && currentPtr != IntPtr.Zero)
            return _cachedValue;

        var newValue = MemoryReadHelper.GetManagedString(memoryReader, currentPtr + 0x4);

        _lastPtr = currentPtr;
        _cachedValue = newValue;

        return newValue;
    }
}