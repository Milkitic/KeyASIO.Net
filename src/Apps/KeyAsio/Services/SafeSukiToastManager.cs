using Avalonia.Threading;
using SukiUI.Toasts;

namespace KeyAsio.Services;

public class SafeSukiToastManager : SukiToastManager, IDisposable
{
    void IDisposable.Dispose()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            base.Dispose();
        }
        else
        {
            try
            {
                Dispatcher.UIThread.Post(base.Dispose);
            }
            catch
            {
                // ignored
            }
        }
    }
}