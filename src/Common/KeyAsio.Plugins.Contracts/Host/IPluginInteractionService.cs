namespace KeyAsio.Plugins.Contracts;

public enum PluginNotificationKind
{
    Information,
    Success,
    Warning,
    Error
}

/// <summary>
/// Host-owned presentation gateway. Plugins may supply their own view object, but
/// they do not receive the application's dialog manager or dispatcher.
/// </summary>
public interface IPluginInteractionService
{
    ValueTask InvokeAsync(Action action);

    void ShowDialog(object content);

    void DismissDialog();

    void ShowMessage(string title, string content);

    void ShowNotification(string title, string content, PluginNotificationKind kind = PluginNotificationKind.Information);
}
