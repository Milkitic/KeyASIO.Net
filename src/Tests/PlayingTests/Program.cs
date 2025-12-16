// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.Utils;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Realtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using Milki.Extensions.Threading;

namespace PlayingTests;

static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        var appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();
        appSettings.Logging.EnableDebugConsole = true;
        appSettings.Realtime.RealtimeEnableMusic = true;
        appSettings.Audio.MasterVolume = 5;
        var context = new SingleSynchronizationContext("AudioPlaybackEngine_STA", true,
            ThreadPriority.AboveNormal);
        var logFactory = LoggerFactory.Create(k => k.AddSimpleConsole());
        var deviceCreationHelper = new AudioDeviceManager(logFactory.CreateLogger<AudioDeviceManager>());
        var cachedAudioFactory = new AudioCacheManager(logFactory.CreateLogger<AudioCacheManager>());
        //var (device, actualDescription) = deviceCreationHelper.CreateDevice(, context);
        var services = new ServiceCollection();
        services.AddSingleton(appSettings);
        services.AddSingleton<GameplaySessionManager>();
        services.AddSingleton<SharedViewModel>();
        services.AddSingleton<GameplayAudioService>();
        services.AddSingleton<BeatmapHitsoundLoader>();
        services.AddSingleton<BackgroundMusicManager>();
        services.AddSingleton<SfxPlaybackService>();
        services.AddSingleton<RealtimeSessionContext>();
        var provider = services.BuildServiceProvider();
        var sharedViewModel = provider.GetRequiredService<SharedViewModel>();
        sharedViewModel.AutoMode = true;

        var audioEngine = provider.GetRequiredService<AudioEngine>();
        audioEngine.EffectVolume = appSettings.Audio.MasterVolume / 100f;
        audioEngine.StartDevice(new DeviceDescription()
        {
            DeviceId = "ASIO4ALL V2",
            FriendlyName = "ASIO4ALL V2",
            WavePlayerType = WavePlayerType.ASIO,
            Latency = 1
        });
        appSettings.Realtime.Playback.BalanceFactor = 0.5f;

        var filenameFull =
            @"C:\Users\milkitic\Downloads\1680421 EBIMAYO - GOODTEK [no video]\EBIMAYO - GOODTEK (yf_bmp) [Maboyu's Another].osu";
        var filename = Path.GetFileName(filenameFull);

        var realtimeModeManager = provider.GetRequiredService<RealtimeSessionContext>();
        realtimeModeManager.BaseMemoryTime = -1;
        realtimeModeManager.PlayMods = Mods.None;
        realtimeModeManager.OsuStatus = OsuMemoryStatus.SongSelection;
        var files = Directory.EnumerateFiles(@"D:\GitHub\Osu-Player\OsuPlayer.Wpf\bin\Debug\Songs\", "*.osu",
            SearchOption.AllDirectories).ToArray();
        int i = 0;
        string? folder = "";
        while (i < files.Length)
        {
            var file = files[i];
            var f = Path.GetDirectoryName(file);
            if (f == folder)
            {
                i++;
                continue;
            }

            folder = f;
            //realtimeModeManager.Beatmap = new Beatmap(0, 0, 0,
            //    @"D:\GitHub\Osu-Player\OsuPlayer.Wpf\bin\Debug\Songs\739119 3L - Spring of Dreams\3L - Spring of Dreams (Trust) [Lunatic].osu");
            Console.WriteLine(file);
            //var k = Console.ReadKey();
            //await realtimeModeManager.StartAsync(filenameFull, filename);
            realtimeModeManager.Beatmap = new BeatmapIdentifier(file);
            if (Console.ReadKey().KeyChar == 'q') break;
            i++;
        }

        Console.WriteLine("Playing");
        realtimeModeManager.OsuStatus = OsuMemoryStatus.Playing;

        var sw = new VariableStopwatch()
        {
            Rate = 1f
        };

        await Task.Delay(1500);

        Task.Run(() =>
        {
            while (true)
            {
                realtimeModeManager.BaseMemoryTime = (int)sw.ElapsedMilliseconds - 1000;
                Thread.Sleep(3);
            }
        });
        sw.Start();

        await Task.Delay(3000);
        sw.Reset();

        var playSessionService = provider.GetRequiredService<GameplaySessionManager>();

        //realtimeModeManager.Stop();
        await playSessionService.StartAsync(filenameFull, filename);
        await Task.Delay(800);
        sw.Restart();

        Console.ReadKey();
        audioEngine.Dispose();
    }
}