using Avalonia;
using KeyAsio.Shared.OsuMemory;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;

namespace MemoryReadingTest
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--console") || args.Contains("-c"))
            {
                RunConsoleMode();
            }
            else
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        private static void RunConsoleMode()
        {
            Console.WriteLine("Starting Memory Reading Test in Console Mode...");
            
            var provider = AppBootstrapper.InitServices();
            var memoryScan = provider.GetRequiredService<MemoryScan>();
            
            AppBootstrapper.ConfigureMemoryScan(provider);

            // Add console logging for events
            memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => 
                Console.WriteLine($"[Status] {pre} -> {current}");
            
            memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => 
                Console.WriteLine($"[Beatmap] {beatmap}");
            
            memoryScan.MemoryReadObject.ModsChanged += (_, mods) => 
                Console.WriteLine($"[Mods] {mods}");
                
            memoryScan.MemoryReadObject.ScoreChanged += (_, score) => 
                Console.WriteLine($"[Score] {score}");

            memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
                Console.WriteLine($"[Combo] {combo}");

            Console.WriteLine("Memory Scan started. Press 'q' to quit.");
            
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q')
                        break;
                }
                Thread.Sleep(100);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
