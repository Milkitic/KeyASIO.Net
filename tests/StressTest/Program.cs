using System.Reflection;
using Coosu.Beatmap;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared;
using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Plugins;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Shared.Sync.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace StressTest;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Setting up Stress Test...");

        var services = new ServiceCollection();

        // Register Logger
        services.AddLogging(builder => { });

        // Register Core Services
        services.AddSingleton<AppSettings>();
        services.AddSingleton<AudioDeviceManager>();
        services.AddSingleton<AudioCacheManager>();
        services.AddSingleton<AudioEngine>(); // We will manipulate this instance later

        // Register Sync Module Services
        services.AddSingleton<SharedViewModel>();
        services.AddSingleton<GameplayAudioService>();
        services.AddSingleton<BeatmapHitsoundLoader>();
        services.AddSingleton<SfxPlaybackService>();
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<GameplaySessionManager>();
        services.AddSingleton<SyncSessionContext>();
        services.AddSingleton<SyncController>();
        services.AddSingleton<SkinManager>();

        var provider = services.BuildServiceProvider();

        // 1. Setup AudioEngine with NoOpWavePlayer
        var audioEngine = provider.GetRequiredService<AudioEngine>();
        SetupNoOpAudioEngine(audioEngine);

        // 2. Setup Context and State
        var ctx = provider.GetRequiredService<SyncSessionContext>();
        var gameplaySessionManager = provider.GetRequiredService<GameplaySessionManager>();
        var audioCacheManager = provider.GetRequiredService<AudioCacheManager>();
        var gameplayAudioService = provider.GetRequiredService<GameplayAudioService>();

        var playingState = new PlayingState(
            provider.GetRequiredService<ILogger<PlayingState>>(),
            provider.GetRequiredService<AppSettings>(),
            audioEngine,
            provider.GetRequiredService<BeatmapHitsoundLoader>(),
            provider.GetRequiredService<SfxPlaybackService>(),
            provider.GetRequiredService<SharedViewModel>(),
            gameplaySessionManager,
            gameplayAudioService
        );
        // 3. Prepare Dummy Data
        Console.WriteLine("Preparing Dummy Data...");

        // Setup OsuFile
        var osuFile = OsuFile.CreateEmpty();
        osuFile.General.Mode = GameMode.Circle;
        osuFile.Difficulty.CircleSize = 4;

        // Manually set OsuFile on GameplaySessionManager
        // OsuFile property has internal setter. We can use reflection or if it's in the same assembly (internals visible to).
        // Since we are in a different project, we need reflection.
        var propOsuFile = typeof(GameplaySessionManager).GetProperty("OsuFile",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        propOsuFile?.SetValue(gameplaySessionManager, osuFile);

        // Setup HitsoundSequencer
        // GameplaySessionManager needs to initialize providers
        // But the constructor doesn't do it. InitializeProviders does.
        // And InitializeProviders needs providers.
        // We can use the ones created by DI if we registered them, but we didn't register IHitsoundSequencer implementations separately in the list above.
        // SyncController usually does this.
        // Let's manually create them.

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var maniaSequencer = new KeyAsio.Shared.Sync.AudioProviders.ManiaHitsoundSequencer(
            loggerFactory.CreateLogger<KeyAsio.Shared.Sync.AudioProviders.ManiaHitsoundSequencer>(),
            provider.GetRequiredService<AppSettings>(),
            ctx,
            audioEngine,
            gameplayAudioService,
            gameplaySessionManager
        );
        var stdSequencer = new KeyAsio.Shared.Sync.AudioProviders.StandardHitsoundSequencer(
            loggerFactory.CreateLogger<KeyAsio.Shared.Sync.AudioProviders.StandardHitsoundSequencer>(),
            provider.GetRequiredService<AppSettings>(),
            ctx,
            audioEngine,
            gameplayAudioService,
            gameplaySessionManager
        );
        var taikoSequencer = new KeyAsio.Shared.Sync.AudioProviders.TaikoHitsoundSequencer(
            loggerFactory.CreateLogger<KeyAsio.Shared.Sync.AudioProviders.TaikoHitsoundSequencer>(),
            provider.GetRequiredService<AppSettings>(),
            ctx,
            audioEngine,
            gameplayAudioService,
            gameplaySessionManager
        );
        var catchSequencer = new KeyAsio.Shared.Sync.AudioProviders.CatchHitsoundSequencer(
            loggerFactory.CreateLogger<KeyAsio.Shared.Sync.AudioProviders.CatchHitsoundSequencer>(),
            provider.GetRequiredService<AppSettings>(),
            ctx,
            audioEngine,
            gameplayAudioService,
            gameplaySessionManager
        );

        gameplaySessionManager.InitializeProviders(stdSequencer, taikoSequencer, catchSequencer, maniaSequencer);

        // Populate BeatmapHitsoundLoader (via PlaybackList/KeyList which are lists in Loader)
        // Access Loader directly
        var loader = provider.GetRequiredService<BeatmapHitsoundLoader>();
        // KeyList is List<PlayableNode>
        // PlaybackList is IReadOnlyList, but backing field is _playbackList.
        // BeatmapHitsoundLoader exposes PlaybackList as read-only.
        // But we can add to KeyList directly.
        // For PlaybackList, we might need reflection to access _playbackList.

        var fieldPlaybackList =
            typeof(BeatmapHitsoundLoader).GetField("_playbackList", BindingFlags.NonPublic | BindingFlags.Instance);
        var playbackList = (List<PlaybackEvent>)fieldPlaybackList!.GetValue(loader)!;

        await audioCacheManager.GetOrCreateOrEmptyFromFileAsync("test.wav", audioEngine.EngineWaveFormat);
        await audioCacheManager.GetOrCreateOrEmptyFromFileAsync("effect.wav", audioEngine.EngineWaveFormat);

        // Add 1000 nodes
        for (int i = 0; i < 1000; i++)
        {
            var offset = i * 10; // Every 10ms

            // Create PlayableNode
            // PlayableNode is likely abstract or simple class.
            // Let's try to instantiate it. It inherits HitsoundNode.
            var node = PlaybackEvent.Create(
                Guid.NewGuid(),
                offset,
                volume: 1f,
                balance: 0f,
                filename: "test.wav",
                resourceOwner: ResourceOwner.Beatmap,
                layer: SampleLayer.Primary); // Key sound
            loader.KeyList.Add(node);
            var playbackNode = PlaybackEvent.Create(
                Guid.NewGuid(),
                offset + 5,
                volume: 0.8f,
                balance: 0f,
                filename: "effect.wav",
                resourceOwner: ResourceOwner.Beatmap,
                layer: SampleLayer.Effects);
            playbackList.Add(playbackNode);
        }

        Console.WriteLine($"Added {loader.KeyList.Count} KeyNodes and {playbackList.Count} PlaybackNodes.");

        // Initialize Sequencer (SeekTo)
        // We need to call ResetNodes on BeatmapHitsoundLoader
        // It is public.
        loader.ResetNodes(gameplaySessionManager.CurrentHitsoundSequencer, 0);

        // Prepare KeyMap for simulation
        var keyMap = new Dictionary<int, List<int>>();
        int keyCount = 4;
        foreach (var node in loader.KeyList)
        {
            var ratio = (node.Balance + 1d) / 2;
            var column = (int)Math.Round(ratio * keyCount - 0.5);
            if (!keyMap.TryGetValue((int)node.Offset, out var list))
            {
                list = new List<int>();
                keyMap[(int)node.Offset] = list;
            }

            list.Add(column);
        }

        // Set Started
        ctx.IsStarted = true;
        ctx.BaseMemoryTime = 0;

        // 4. Run Stress Test
        Console.WriteLine("Starting Stress Test. Press 'Q' to stop...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        long totalUpdates = 0;
        int songLength = 10000; // 10 seconds loop

        var interactionBuffer = new List<PlaybackInfo>(64);

        await gameplayAudioService.AddHitsoundCacheAsync(loader.KeyList[0], ".", ".", audioEngine.EngineWaveFormat);
        await gameplayAudioService.AddHitsoundCacheAsync(playbackList[0], ".", ".", audioEngine.EngineWaveFormat);

        while (true)
        {
            //if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
            //{
            //    break;
            //}

            int oldMs = (int)((totalUpdates == 0 ? 0 : totalUpdates - 1) % songLength);
            int newMs = (int)(totalUpdates % songLength);

            ctx.BaseMemoryTime = newMs; // Update context

            // Call OnPlayTimeChanged
            playingState.OnTick(ctx, oldMs, newMs, false);

            // Simulate key presses
            // Check if there are keys mapped at this timestamp
            if (keyMap.TryGetValue(newMs, out var columns))
            {
                foreach (var col in columns)
                {
                    interactionBuffer.Clear();
                    stdSequencer.ProcessInteraction(interactionBuffer, col, 4);
                    foreach (var playbackInfo in interactionBuffer)
                    {
                        // Dispatch to SfxPlaybackService
                        var sfxService = provider.GetRequiredService<SfxPlaybackService>();
                        sfxService.DispatchPlayback(playbackInfo);
                    }
                }
            }

            if (totalUpdates % 1000 == 0)
            {
                //Console.WriteLine($"Processed {totalUpdates} updates (Current time: {newMs}ms)...");
            }

            //Thread.Sleep(2);
            totalUpdates++;
        }

        sw.Stop();
        Console.WriteLine($"Test Completed in {sw.ElapsedMilliseconds}ms.");
        if (totalUpdates > 0)
        {
            Console.WriteLine($"Total updates: {totalUpdates}");
            Console.WriteLine($"Average time per update: {(double)sw.ElapsedMilliseconds / totalUpdates}ms");
        }
    }

    private static void SetupNoOpAudioEngine(AudioEngine engine)
    {
        // 1. Create NoOpWavePlayer
        var player = new NoOpWavePlayer();

        // 2. Set CurrentDevice via reflection
        var propCurrentDevice =
            typeof(AudioEngine).GetProperty("CurrentDevice", BindingFlags.Public | BindingFlags.Instance);
        propCurrentDevice?.SetValue(engine, player);

        // 3. Initialize Mixers
        // We need to call StartDevice logic partially or replicate it.
        // StartDevice initializes mixers.
        // If we can't call StartDevice because it creates real device, we must replicate initialization.
        // However, mixers have private setters.

        var waveFormat = new WaveFormat(44100, 2);
        var ieeeFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        // Set Formats
        SetPrivateProperty(engine, "SourceWaveFormat", waveFormat);
        SetPrivateProperty(engine, "EngineWaveFormat", ieeeFormat);

        // Create Mixers
        var rootMixer = new EnhancedMixingSampleProvider(ieeeFormat) { ReadFully = true };
        var effectMixer = new EnhancedMixingSampleProvider(ieeeFormat) { ReadFully = true };
        var musicMixer = new EnhancedMixingSampleProvider(ieeeFormat) { ReadFully = true };

        SetPrivateProperty(engine, "RootMixer", rootMixer);
        SetPrivateProperty(engine, "EffectMixer", effectMixer);
        SetPrivateProperty(engine, "MusicMixer", musicMixer);

        // Wire up mixers (as done in StartDevice)
        // We need access to private fields _effectVolumeSampleProvider, etc. to set their Source.

        var fieldEffectVol = typeof(AudioEngine).GetField("_effectVolumeSampleProvider",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldMusicVol = typeof(AudioEngine).GetField("_musicVolumeSampleProvider",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldMainVol =
            typeof(AudioEngine).GetField("_mainVolumeSampleProvider", BindingFlags.NonPublic | BindingFlags.Instance);

        var effectVol = (EnhancedVolumeSampleProvider)fieldEffectVol!.GetValue(engine)!;
        var musicVol = (EnhancedVolumeSampleProvider)fieldMusicVol!.GetValue(engine)!;
        var mainVol = (EnhancedVolumeSampleProvider)fieldMainVol!.GetValue(engine)!;

        effectVol.Source = effectMixer;
        musicVol.Source = musicMixer;

        rootMixer.AddMixerInput(effectVol);
        rootMixer.AddMixerInput(musicVol);

        mainVol.Source = rootMixer;

        // Set RootSampleProvider
        SetPrivateProperty(engine, "RootSampleProvider", mainVol);

        // Init player with the provider (NoOp player Init does nothing usually, but we should call it)
        player.Init(mainVol);
        player.Play();
    }

    private static void SetPrivateProperty(object obj, string propName, object value)
    {
        var prop = obj.GetType()
            .GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        prop?.SetValue(obj, value);
    }
}

// NoOp WavePlayer
public class NoOpWavePlayer : IWavePlayer
{
    public WaveFormat OutputWaveFormat { get; set; } = new WaveFormat(44100, 2);
    public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
    public float Volume { get; set; } = 1.0f;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public void Init(ISampleProvider sampleProvider)
    {
        // Do nothing
    }

    public void Init(IWaveProvider waveProvider)
    {
        // Do nothing
    }

    public void Play()
    {
        PlaybackState = PlaybackState.Playing;
    }

    public void Stop()
    {
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Pause()
    {
        PlaybackState = PlaybackState.Paused;
    }

    public void Dispose()
    {
        Stop();
    }
}