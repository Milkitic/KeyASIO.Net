// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KeyAsio.Gui;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using Milki.Extensions.MixPlayer.Utilities;

var appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();
appSettings.Debugging = true;

SharedViewModel.Instance.AudioPlaybackEngine = new AudioPlaybackEngine(new DeviceDescription()
{
    WavePlayerType = WavePlayerType.WASAPI,
    Latency = 1
});
SharedViewModel.Instance.LatencyTestMode = true;
appSettings.RealtimeOptions.BalanceFactor = 0.5f;

var filenameFull = @"C:\Users\milkitic\Desktop\1002455 supercell - Giniro Hikousen  (Ttm bootleg Edit)\supercell - Giniro Hikousen  (Ttm bootleg Edit) (yf_bmp) [7K Hyper].osu";
var filename = Path.GetFileName(filenameFull);

var realtimeModeManager = new RealtimeModeManager();
await realtimeModeManager.StartAsync(filenameFull, filename);

var sw = new VariableStopwatch()
{
    Rate = 1f
};

Task.Run(() =>
{
    while (true)
    {
        realtimeModeManager.PlayTime = (int)sw.ElapsedMilliseconds;
        Thread.Sleep(2);
    }
});
sw.Start();

Console.ReadKey();