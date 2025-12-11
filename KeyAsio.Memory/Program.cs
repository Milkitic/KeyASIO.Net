using System.Diagnostics;
using KeyAsio.Memory.Configuration;

namespace KeyAsio.Memory;

public class Program
{
    private static MemoryContext? _ctx;
    private static SigScan? _sigScan;
    private static readonly object _lock = new();

    // 指向源文件的绝对路径，方便演示动态修改
    private const string ConfigPath = @"e:\Working\GitHub\KeyAsio.Net\KeyAsio.Memory\Configuration\rules.json";

    public static async Task Main(string[] args)
    {
        PowerThrottling.DisableThrottling();

        await DirectScan.Perform();

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

        // 高频读取线程 (模拟原有的 AudioTime 读取)
        var audioTask = Task.Factory.StartNew(() =>
        {
            using var scope = new HighPrecisionTimerScope();
            int oldAudioTime = int.MinValue;

            while (!cts.IsCancellationRequested)
            {
                lock (_lock)
                {
                    if (_ctx != null)
                    {
                        var audioTime = _ctx.GetValue<int>("AudioTime");
                        if (audioTime.HasValue && oldAudioTime != audioTime.Value)
                        {
                            // 可以在这里输出，但为了不刷屏，暂时注释掉
                            // Console.WriteLine($"AudioTime: {audioTime}");
                            oldAudioTime = audioTime.Value;
                        }
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

            lock (_lock)
            {
                if (_ctx != null)
                {
                    try
                    {
                        var data = new OsuData();
                        _ctx.Populate(data);

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
            lock (_lock)
            {
                Console.WriteLine("Loading Configuration...");
                var profile = MemoryProfile.Load(ConfigPath);
                _ctx = new MemoryContext(_sigScan!, profile);
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