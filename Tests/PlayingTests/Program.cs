// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
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
        var device = DeviceCreationHelper.CreateDevice(out _, new DeviceDescription()
        {
            DeviceId = "ASIO4ALL V2",
            FriendlyName = "ASIO4ALL V2",
            WavePlayerType = WavePlayerType.ASIO,
            Latency = 1
        }, context);
        SharedViewModel.Instance.AudioPlaybackEngine = new AudioEngine(device);
        SharedViewModel.Instance.LatencyTestMode = true;
        appSettings.RealtimeOptions.BalanceFactor = 0.5f;

        var filenameFull = @"C:\Users\milkitic\Downloads\1680421 EBIMAYO - GOODTEK [no video]\EBIMAYO - GOODTEK (yf_bmp) [Maboyu's Another].osu";
        var filename = Path.GetFileName(filenameFull);

        var realtimeModeManager = new RealtimeModeManager()
        {
            PlayTime = -1,
            PlayMods = ModsInfo.Mods.Nightcore
        };
        await realtimeModeManager.StartAsync(filenameFull, filename);

        var sw = new VariableStopwatch()
        {
            Rate = 1.5f
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