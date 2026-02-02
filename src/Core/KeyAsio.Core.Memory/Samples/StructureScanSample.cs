using System.Diagnostics;
using System.Runtime.Versioning;
using KeyAsio.Core.Memory.Configuration;
using KeyAsio.Core.Memory.Utils;

namespace KeyAsio.Core.Memory.Samples;

internal static class StructureScanSample
{
    private static MemoryContext<SampleOsuData>? _ctx;
    private static SigScan? _sigScan;
    private static readonly Lock s_lock = new();

    private const string ConfigPath = @"";

    [SupportedOSPlatform("windows8.0")]
    public static async Task Perform()
    {
        PowerThrottling.DisableThrottling();

        await DirectScanSample.Perform();

        var process = Process.GetProcessesByName("osu!").FirstOrDefault();
        if (process == null)
        {
            Console.WriteLine("osu! process not found. Please start osu! and restart this program.");
            return;
        }

        _sigScan = new SigScan(process);

        // 初始加载配置
        ReloadConfig();

        // 启动文件监控
        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(ConfigPath)!, Path.GetFileName(ConfigPath));
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Changed += (s, e) =>
        {
            // 简单的防抖动
            Thread.Sleep(100);
            ReloadConfig();
        };
        watcher.EnableRaisingEvents = true;

        Console.WriteLine("Memory Reader Started.");
        Console.WriteLine($"Watching config file: {ConfigPath}");
        Console.WriteLine("Press 'Q' to exit.");

        var cts = new CancellationTokenSource();

        var data = new SampleOsuData();
        var audioTask = Task.Factory.StartNew(() =>
        {
            using var scope = new HighPrecisionTimerScope();

            while (!cts.IsCancellationRequested)
            {
                lock (s_lock)
                {
                    if (_ctx != null)
                    {
                        _ctx.BeginUpdate();
                        _ctx.Populate(data);
                    }
                }

                Thread.Sleep(1);
            }
        }, TaskCreationOptions.LongRunning);

        // 主循环：定期刷新显示完整数据
        while (!cts.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q) break;
            }

            lock (s_lock)
            {
                if (_ctx != null)
                {
                    try
                    {
                        Console.Clear();
                        Console.WriteLine("=== KeyAsio Memory Reader (Declarative Framework) ===");
                        Console.WriteLine($"Config Source: {ConfigPath}");
                        Console.WriteLine("--------------------------------------------------");
                        Console.WriteLine(data.ToString());
                        Console.WriteLine("--------------------------------------------------");
                        Console.WriteLine($"Folder: {data.FolderName}");
                        Console.WriteLine($"File:   {data.OsuFileName}");
                        Console.WriteLine($"MD5:    {data.MD5}");
                        Console.WriteLine($"Mods:   {data.Mods}");
                        Console.WriteLine($"Status: {data.Status} (Raw: {data.RawStatus})");
                        Console.WriteLine($"GameMode: {data.GameMode}");
                        Console.WriteLine($"Retries:  {data.Retries}");
                        Console.WriteLine($"User:     {data.Username}");
                        Console.WriteLine($"Score:    {data.Score}");
                        Console.WriteLine($"Combo:    {data.Combo}");
                        Console.WriteLine("--------------------------------------------------");
                        Console.WriteLine("Modify the rules.json file to update offsets dynamically!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading memory: {ex.Message}");
                    }
                }
            }

            await Task.Delay(500); // 0.5秒刷新一次界面
        }

        cts.Cancel();
        try
        {
            await audioTask;
        }
        catch
        {
        }

        _sigScan.Dispose();
    }

    private static void ReloadConfig()
    {
        try
        {
            lock (s_lock)
            {
                Console.WriteLine("Loading Configuration...");
                var profile = MemoryProfile.Load(ConfigPath);
                _ctx = new MemoryContext<SampleOsuData>(_sigScan!, profile);
                _ctx.Scan();
                Console.WriteLine("Configuration Loaded Successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }
    }
}