using System.Diagnostics;
using Avalonia;
using KeyAsio.Audio;
using KeyAsio.MemoryReading;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Configuration;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Utils;
using KeyAsio.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using NLog.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace KeyAsio;

internal sealed class Program
{
    internal static IHost Host { get; private set; } = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        using var mutex = new Mutex(true, "KeyAsio.Net", out bool createNew);
        if (!createNew)
        {
            var process = Process
                .GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                .FirstOrDefault(k => k.Id != Environment.ProcessId && k.MainWindowHandle != IntPtr.Zero);
            if (process == null || !OperatingSystem.IsWindowsVersionAtLeast(5)) return;
            PInvoke.ShowWindow((HWND)process.MainWindowHandle, SHOW_WINDOW_CMD.SW_SHOW);
            PInvoke.SetForegroundWindow((HWND)process.MainWindowHandle);
            return;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(6))
        {
            throw new PlatformNotSupportedException(
            $"Current OS version {Environment.OSVersion.Version} is not supported. " +
            $"Requires Windows Vista or later.");
        }

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddNLog("nlog.config");
            })
            .ConfigureServices(services => services
                .AddSingleton<App>()
                .AddSingleton<UpdateService>()
                .AddSingleton<MemoryScan>()
                .AddAudioModule()
                .AddRealtimeModule()
                .AddGuiModule()
                .AddSingleton(provider => ConfigurationFactory.GetConfiguration<AppSettings>(
                    ".", "appsettings.yaml", MyYamlConfigurationConverter.Instance)))
            .Build();
        try
        {
            await Host.RunAsync();
        }
        finally
        {
            Host?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            //.WithInterFont()
            .LogToTrace();

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = (Exception)e.ExceptionObject;
        MessageBox.Error(AppExtensions.CurrentDesktop?.MainWindow == null
                ? "Unhandled error occurs while starting KeyASIO..."
                : "Unhandled error occurs while KeyASIO is running...", "Program critical error",
            title: "KeyASIO.Net", detail: exception.ToFullTypeMessage());
    }
}