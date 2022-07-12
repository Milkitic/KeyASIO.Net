// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyAsio.Gui;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Realtime;
using KeyAsio.Gui.Waves;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.Threading;
using Milki.Extensions.MixPlayer.Utilities;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider.Mods;

namespace PlayingTests;

static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        var appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();
        appSettings.Debugging = true;
        //appSettings.RealtimeOptions.IgnoreMusicTrack = true;
        var context = (SynchronizationContext)new StaSynchronizationContext("AudioPlaybackEngine_STA");
        var device = DeviceCreationHelper.CreateDevice(out _, null, context);
        SharedViewModel.Instance.AudioEngine = new AudioEngine(device);
        SharedViewModel.Instance.LatencyTestMode = true;
        appSettings.RealtimeOptions.BalanceFactor = 0.5f;

        var filenameFull = @"E:\Games\osu!\Songs\197085 Kayano Ai - Oracion(TV-Size)\Kayano Ai - Oracion(TV-Size) ([AyanoTatemaya]) [7K MX].osu";
        var filename = Path.GetFileName(filenameFull);

        var realtimeModeManager = new RealtimeModeManager()
        {
            PlayTime = -1,
            PlayMods = ModsInfo.Mods.None
        };
        //realtimeModeManager.OsuStatus = OsuListenerManager.OsuStatus.SelectSong;
        //var files = Directory.EnumerateFiles(@"D:\GitHub\Osu-Player\OsuPlayer.Wpf\bin\Debug\Songs\", "*.osu",
        //    SearchOption.AllDirectories).ToArray();
        //int i = 0;
        //string? folder = "";
        //while (i < files.Length)
        //{
        //    var file = files[i];
        //    var f = Path.GetDirectoryName(file);
        //    if (f == folder)
        //    {
        //        i++;
        //        continue;
        //    }

        //    folder = f;
        //    //realtimeModeManager.Beatmap = new Beatmap(0, 0, 0,
        //    //    @"D:\GitHub\Osu-Player\OsuPlayer.Wpf\bin\Debug\Songs\739119 3L - Spring of Dreams\3L - Spring of Dreams (Trust) [Lunatic].osu");
        //    Console.WriteLine(file);
        //    //var k = Console.ReadKey();
        //    //await realtimeModeManager.StartAsync(filenameFull, filename);
        //    realtimeModeManager.Beatmap = new Beatmap(0, 0, 0, file);
        //    if (Console.ReadKey().KeyChar == 'q') break;
        //    i++;
        //}

        Console.WriteLine("Playing");
        realtimeModeManager.OsuStatus = OsuListenerManager.OsuStatus.Playing;

        var sw = new VariableStopwatch()
        {
            Rate = 1f
        };

        await Task.Delay(1500);

        Task.Run(() =>
        {
            while (true)
            {
                realtimeModeManager.PlayTime = (int)sw.ElapsedMilliseconds - 1000;
                Thread.Sleep(3);
            }
        });
        sw.Start();

        await Task.Delay(3000);
        sw.Reset();
        //realtimeModeManager.Stop();
        await Task.Delay(500);
        sw.Restart();
        await realtimeModeManager.StartAsync(filenameFull, filename);

        Console.ReadKey();
        device.Dispose();
    }
}