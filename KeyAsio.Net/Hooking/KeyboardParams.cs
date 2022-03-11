namespace KeyAsio.Net.Hooking;

internal struct KeyboardParams
{
    public IntPtr wParam;
    public int vkCode;

    public KeyboardParams(IntPtr wParam, int vkCode)
    {
        this.wParam = wParam;
        this.vkCode = vkCode;
    }
}