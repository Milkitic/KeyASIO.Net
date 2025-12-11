using Windows.Win32;

namespace KeyAsio.Memory;

public ref struct HighPrecisionTimerScope : IDisposable
{
    private bool _disposed;

    public HighPrecisionTimerScope()
    {
        PInvoke.timeBeginPeriod(1);
    }

    public void Dispose()
    {
        if (_disposed) return;

        PInvoke.timeEndPeriod(1);
        _disposed = true;
    }
}