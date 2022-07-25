using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Realtime;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Windows;
using Microsoft.Extensions.Logging;
using OsuRTDataProvider.Listen;
using OrtdpLogger = OsuRTDataProvider.Logger;
using OrtdpSetting = OsuRTDataProvider.Setting;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly ILogger Logger = LogUtils.GetLogger("Application");

    [STAThread]
    internal static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        CreateApplication();
    }

    private static void CreateApplication()
    {
        var mutex = new Mutex(true, "KeyAsio.Net", out bool createNew);
        if (!createNew)
        {
            var process = Process
                .GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                .FirstOrDefault(k => k.Id != Environment.ProcessId && k.MainWindowHandle != IntPtr.Zero);
            if (process == null) return;
            ProcessUtils.ShowWindow(process.MainWindowHandle, ProcessUtils.SW_SHOW);
            ProcessUtils.SetForegroundWindow(process.MainWindowHandle);
            return;
        }

        using var _ = new EmbeddedSentryConfiguration(options =>
        {
            options.HttpProxy = HttpClient.DefaultProxy;
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
            options.DefaultTags.Add("os.detail", HardwareInformationHelper.GetOsInformation());
            options.DefaultTags.Add("processor", HardwareInformationHelper.GetProcessorInformation());
            options.DefaultTags.Add("total_memory", HardwareInformationHelper.GetPhysicalMemory());
        });

        try
        {
            Logger.Info("Application started.", true);
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        finally
        {
            mutex.ReleaseMutex();
            Logger.Info("Application stopped.", true);
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = (Exception)e.ExceptionObject;
        if (Application.Current?.MainWindow == null)
        {
            MessageBox.Show(exception.ToFullTypeMessage(), "KeyASIO startup error ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(exception.ToFullTypeMessage(), "KeyASIO runtime error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        var settings = ConfigurationFactory.GetConfiguration<AppSettings>();

        if (settings.Debugging)
        {
            ConsoleManager.Show();
        }

        if (string.IsNullOrWhiteSpace(settings.OsuFolder))
        {
            SkinManager.Instance.CheckOsuRegistry();
        }

        SkinManager.Instance.ListenPropertyChanging();
        SkinManager.Instance.RefreshSkinInBackground();
        if (settings.RealtimeOptions.RealtimeMode)
        {
            try
            {
                var player = EncodeUtils.FromBase64String(settings.PlayerBase64, Encoding.ASCII);
                RealtimeModeManager.Instance.Username = player;
            }
            catch
            {
                // ignored
            }

            OrtdpLogger.SetLoggerFactory(LogUtils.LoggerFactory);
            OrtdpSetting.DisableProcessNotFoundInformation = true;
            OrtdpSetting.ListenInterval = 3;
            var manager = new OsuListenerManager();
            manager.OnPlayerChanged += player => RealtimeModeManager.Instance.Username = player;
            manager.OnModsChanged += modsInfo => RealtimeModeManager.Instance.PlayMods = modsInfo.Mod;
            manager.OnComboChanged += combo => RealtimeModeManager.Instance.Combo = combo;
            manager.OnScoreChanged += score => RealtimeModeManager.Instance.Score = score;
            manager.OnPlayingTimeChanged += playTime => RealtimeModeManager.Instance.PlayTime = playTime;
            manager.OnBeatmapChanged += beatmap => RealtimeModeManager.Instance.Beatmap = beatmap;
            manager.OnStatusChanged += (pre, current) => RealtimeModeManager.Instance.OsuStatus = current;
            manager.Start();
            RealtimeModeManager.Instance.OsuListenerManager = manager;
            SkinManager.Instance.ListenToProcess();
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unhandled Exception (Dispatcher): " + e.Exception.Message, true);
        e.Handled = true;
    }
}