using System.Diagnostics;
using Avalonia;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Memory.Utils;
using KeyAsio.Lang;
using KeyAsio.Secrets;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Configuration;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Utils;
using KeyAsio.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using NLog.Extensions.Logging;
using Sentry.Extensibility;
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

        var configFolder = ".";
        if (!IsDirectoryWritable(configFolder))
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyAsio");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }

            // Migrate existing config if local is readable but not writable
            var localConfig = Path.Combine(configFolder, "appsettings.yaml");
            var appDataConfig = Path.Combine(appData, "appsettings.yaml");
            if (File.Exists(localConfig) && !File.Exists(appDataConfig))
            {
                try
                {
                    File.Copy(localConfig, appDataConfig);
                }
                catch
                {
                    // Ignore copy errors
                }
            }

            configFolder = appData;
        }

        var appSettings = ConfigurationFactory.GetConfiguration<AppSettings>(
            configFolder, "appsettings.yaml", MyYamlConfigurationConverter.Instance);
        Mutex? mutex = null;
        if (!appSettings.General.AllowMultipleInstance)
        {
            mutex = new Mutex(true, "KeyAsio.Net", out bool createNew);
            if (!createNew)
            {
                try
                {
                    await using var client = new System.IO.Pipes.NamedPipeClientStream(".", IpcService.PipeName, System.IO.Pipes.PipeDirection.Out);
                    await client.ConnectAsync(1000);
                    await using var writer = new StreamWriter(client);
                    await writer.WriteAsync("SHOW_WINDOW");
                    await writer.FlushAsync();
                    return;
                }
                catch
                {
                    // Ignore
                }

                // logic to bring existing V3 instance to foreground
                var processName = Process.GetCurrentProcess().ProcessName;
                var process = Process
                    .GetProcessesByName(processName)
                    .FirstOrDefault(k => k.Id != Environment.ProcessId && k.MainWindowHandle != IntPtr.Zero);
                if (process == null || !OperatingSystem.IsWindowsVersionAtLeast(5)) return;
                PInvoke.ShowWindow((HWND)process.MainWindowHandle, SHOW_WINDOW_CMD.SW_RESTORE);
                PInvoke.SetForegroundWindow((HWND)process.MainWindowHandle);
                return;
            }
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(6))
        {
            throw new PlatformNotSupportedException(
                string.Format(SR.Error_OSNotSupported, Environment.OSVersion.Version));
        }

        if (appSettings.Logging.EnableDebugConsole)
        {
            ConsoleManager.Show();
        }

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddNLog("nlog.config");
                logging.AddSentry(options =>
                {
                    EmbeddedSentryConfiguration.Configure(options);
                    options.MinimumEventLevel = LogLevel.Error;
                    options.HttpProxy = HttpClient.DefaultProxy;
                    options.ShutdownTimeout = TimeSpan.FromSeconds(5);
#if !RELEASE
                    var diagnosticLoggerFactory = LoggerFactory.Create(builder =>
                    {
                        builder.AddNLog("nlog.config");
                        builder.SetMinimumLevel(LogLevel.Debug);
                    });
                    options.DiagnosticLogger =
                        new MicrosoftDiagnosticLogger(diagnosticLoggerFactory.CreateLogger("SentryDiagnostic"));
#endif
                });
            })
            .ConfigureServices(services => services
                .AddSingleton<App>()
                .AddSingleton<UpdateService>()
                .AddSingleton<MemoryScan>()
                .AddSingleton<MemorySyncBridge>()
                .AddSingleton<ISentryEventProcessor, KeyAsioSentryEventProcessor>()
                .AddAudioModule()
                .AddSyncModule()
                .AddGuiModule()
                .AddSingleton(appSettings))
            .Build();

        var processors = Host.Services.GetServices<ISentryEventProcessor>();
        SentrySdk.ConfigureScope(scope =>
        {
            foreach (var processor in processors)
            {
                scope.AddEventProcessor(processor);
            }
        });

        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(8))
                PowerThrottling.DisableThrottling();

            await Host.RunAsync();
        }
        finally
        {
            Host?.Dispose();
            mutex?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = (Exception)e.ExceptionObject;
        MessageBox.Error(AppExtensions.CurrentDesktop?.MainWindow == null
                ? "Unhandled error occurs while starting KeyASIO..."
                : "Unhandled error occurs while KeyASIO is running...", "Program critical error",
            title: "KeyASIO.Net", detail: exception.ToFullTypeMessage());
    }

    private static bool IsDirectoryWritable(string dirPath)
    {
        try
        {
            using (File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
            { }
            return true;
        }
        catch
        {
            return false;
        }
    }
}