using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using KeyAsio.Audio;
using KeyAsio.Gui.Utils;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Configuration;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using NLog.Extensions.Logging;

namespace KeyAsio.Gui;

public static class Program
{
    internal static IHost Host { get; private set; } = null!;

    internal static async Task Main()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        //AssemblyLoadContext.Default.Resolving += ResolveAssembly;
        RedirectLogs();
        using var mutex = new Mutex(true, "KeyAsio.Net", out bool createNew);
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

        await CreateApplicationAsync().ConfigureAwait(true);
    }

    private static async Task CreateApplicationAsync()
    {
        using var _ = new EmbeddedSentryConfiguration(options =>
        {
            options.HttpProxy = HttpClient.DefaultProxy;
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
            // todo: Add hardware info tags
            //options.DefaultTags.Add("os.detail", HardwareInformationHelper.GetOsInformation());
            //options.DefaultTags.Add("processor", HardwareInformationHelper.GetProcessorInformation());
            //options.DefaultTags.Add("total_memory", HardwareInformationHelper.GetPhysicalMemory());
        });
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddNLog("nlog.config");
            })
            .ConfigureServices(services => services
                .AddSingleton<App>()
                .AddSingleton<Updater>()
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
}