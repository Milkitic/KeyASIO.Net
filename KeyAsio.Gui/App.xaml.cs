using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Windows;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Configuration;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer;
using NLog.Extensions.Logging;
using OrtdpLogger = KeyAsio.MemoryReading.Logger;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly KeyAsio.MemoryReading.Logging.ILogger Logger = LogUtils.GetLogger("Application");
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialized.");

    [STAThread]
    internal static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        //AssemblyLoadContext.Default.Resolving += ResolveAssembly;
        RedirectLogs();
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
            //options.DefaultTags.Add("os.detail", HardwareInformationHelper.GetOsInformation());
            //options.DefaultTags.Add("processor", HardwareInformationHelper.GetProcessorInformation());
            //options.DefaultTags.Add("total_memory", HardwareInformationHelper.GetPhysicalMemory());
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

    private static void RedirectLogs()
    {
        var configFile = Path.Combine(Environment.CurrentDirectory, "bin", "nlog.config");
        if (!File.Exists(configFile)) return;
        Console.WriteLine("Found File: " + configFile);
        const string ns = "http://www.nlog-project.org/schemas/NLog.xsd";
        XDocument xDocument;
        bool changed = false;
        using (var fs = File.Open(configFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            xDocument = XDocument.Load(fs);
            var xe_targets = xDocument.Root?.Element(XName.Get("targets", ns));
            if (xe_targets != null)
            {
                var targets = xe_targets.Elements(XName.Get("target", ns));
                foreach (var xe_target in targets)
                {
                    var xa_fileName = xe_target.Attribute("fileName");
                    if (xa_fileName == null || !xa_fileName.Value.StartsWith("logs/")) continue;
                    var value = xa_fileName.Value;
                    xa_fileName.Value = "../" + xa_fileName.Value;
                    changed = true;
                    Console.WriteLine($"Redirected \"{value}\" to \"{xa_fileName.Value}\"");
                }
            }
        }

        if (!changed) return;
        xDocument.DescendantNodes().OfType<XComment>().Remove();
        using var fsw = new StreamWriter(configFile, Encoding.UTF8, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None
        });
        using var xmlWriter = XmlWriter.Create(fsw, new XmlWriterSettings
        {
            Indent = true
        });
        xDocument.Save(xmlWriter);
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var originalPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(originalPath)) return context.LoadFromAssemblyPath(originalPath);
        var pathMaybe = Path.Combine(AppContext.BaseDirectory, "bin", $"{assemblyName.Name}.dll");
        return File.Exists(pathMaybe) ? context.LoadFromAssemblyPath(pathMaybe) : null;
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
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddNLog("nlog.config");
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(provider =>
                    ConfigurationFactory.GetConfiguration<AppSettings>(MyYamlConfigurationConverter.Instance, "."));
                services.AddSingleton(SharedViewModel.Instance);
                services.AddSingleton(SkinManager.Instance);
                services.AddSingleton(RealtimeModeManager.Instance);
                services.AddTransient<DeviceWindowViewModel>();
                services.AddTransient<MainWindow>();
                services.AddTransient<DeviceWindow>();
                services.AddTransient<LatencyGuideWindow>();
                services.AddTransient<RealtimeOptionsWindow>();
            })
            .Build();

        Configuration.Instance.SetLogger(_host.Services.GetRequiredService<ILoggerFactory>());
        NLogDevice.RegisterDefault();

        UiDispatcher.SetUiSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        var settings = _host.Services.GetRequiredService<AppSettings>();

        if (settings.Debugging)
        {
            ConsoleManager.Show();
        }

        if (string.IsNullOrWhiteSpace(settings.OsuFolder))
        {
            SkinManager.Instance.CheckOsuRegistry();
        }

        SkinManager.Instance.ListenPropertyChanging();
        _ = SkinManager.Instance.RefreshSkinInBackground();
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
            MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.Username = player);
            MemoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.PlayMods = mods);
            MemoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.Combo = combo);
            MemoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.Score = score);
            MemoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.IsReplay = isReplay);
            MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.LastFetchedPlayTime = playTime);
            MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.Beatmap = beatmap);
            MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
                Dispatcher.InvokeAsync(() => RealtimeModeManager.Instance.OsuStatus = current);
            MemoryScan.Start(settings.RealtimeOptions.GeneralScanInterval, settings.RealtimeOptions.TimingScanInterval);
            SkinManager.Instance.ListenToProcess();
        }

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispatcher_UnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unhandled Exception (Dispatcher): " + e.Exception.Message, true);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // ignored
        }
        finally
        {
            _host?.Dispose();
        }

        base.OnExit(e);
    }
}