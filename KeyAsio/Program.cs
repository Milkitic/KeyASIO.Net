using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace KeyAsio;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = (Exception)e.ExceptionObject;
        if (((IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime)?.MainWindow == null)
        {
            MsgDialogWin32.Error("Unhandled error occurs while starting KeyASIO...", "Program critical error",
                title: "KeyASIO.Net", detail: exception.ToString());
        }
        else
        {
            MsgDialogWin32.Error("Unhandled error occurs while KeyASIO is running...", "Program critical error",
                title: "KeyASIO.Net", detail: exception.ToString());
        }
    }
}