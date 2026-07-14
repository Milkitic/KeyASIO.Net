using Avalonia.Threading;
using KeyAsio.Application.Abstractions;

namespace KeyAsio.Services;

public sealed class AvaloniaApplicationDispatcher : IApplicationDispatcher
{
    public async Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await Dispatcher.UIThread.InvokeAsync(action);
    }
}
