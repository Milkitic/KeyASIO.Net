namespace KeyAsio.Memory;

public class CachedStringReader
{
    private IntPtr _lastPtr = IntPtr.Zero;
    private string _cachedValue = string.Empty;

    public bool TryGet(IMemoryReader memoryReader, IntPtr stringRefAddr, out string result)
    {
        if (!MemoryReadHelper.TryGetPointer(memoryReader, stringRefAddr, out var currentPtr))
        {
            result = string.Empty;
            return false;
        }

        if (currentPtr == _lastPtr && currentPtr != IntPtr.Zero)
        {
            result = _cachedValue;
            return true;
        }

        if (MemoryReadHelper.TryGetManagedString(memoryReader, stringRefAddr, out var newValue))
        {
            _lastPtr = currentPtr;
            _cachedValue = newValue;
            result = newValue;
            return true;
        }

        result = string.Empty;
        return false;
    }

    public string Get(IMemoryReader memoryReader, IntPtr stringRefAddr)
    {
        TryGet(memoryReader, stringRefAddr, out var result);
        return result;
    }
}