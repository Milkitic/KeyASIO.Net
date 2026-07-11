using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using KeyAsio.Plugins.Contracts;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace KeyAsio.Services;

public sealed class PluginInteractionService : IPluginInteractionService
{
    private readonly ISukiDialogManager _dialogs;
    private readonly ISukiToastManager _toasts;

    public PluginInteractionService(ISukiDialogManager dialogs, ISukiToastManager toasts)
    {
        _dialogs = dialogs;
        _toasts = toasts;
    }

    public async ValueTask InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    public void ShowDialog(object content) => Dispatcher.UIThread.Post(() =>
        _dialogs.CreateDialog().WithContent(content).TryShow());

    public void DismissDialog() => Dispatcher.UIThread.Post(_dialogs.DismissDialog);

    public void ShowMessage(string title, string content) => Dispatcher.UIThread.Post(() =>
        _dialogs.CreateDialog()
            .WithTitle(title)
            .WithContent(content)
            .WithActionButton("OK", _ => { }, true)
            .TryShow());

    public void ShowNotification(
        string title,
        string content,
        PluginNotificationKind kind = PluginNotificationKind.Information) =>
        Dispatcher.UIThread.Post(() =>
            _toasts.CreateToast()
                .WithTitle(title)
                .WithContent(content)
                .OfType(kind switch
                {
                    PluginNotificationKind.Success => NotificationType.Success,
                    PluginNotificationKind.Warning => NotificationType.Warning,
                    PluginNotificationKind.Error => NotificationType.Error,
                    _ => NotificationType.Information
                })
                .Queue());
}
