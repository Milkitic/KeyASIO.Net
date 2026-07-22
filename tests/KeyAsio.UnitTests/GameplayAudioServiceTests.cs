using System.Text;
using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.OsuAudio.Hitsounds;
using KeyAsio.Sync;
using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class GameplayAudioServiceTests : IDisposable
{
    private readonly string _temporaryFolder = Directory.CreateTempSubdirectory().FullName;

    [Fact]
    public async Task MissingComboBreak_IsRetriedWhenDefaultResourcesBecomeAvailable()
    {
        var beatmapFolder = Directory.CreateDirectory(Path.Combine(_temporaryFolder, "beatmap")).FullName;
        var skinFolder = Directory.CreateDirectory(Path.Combine(_temporaryFolder, "skin-without-combobreak")).FullName;
        var runtimeState = new TestPlaybackRuntimeState(skinFolder);
        var skinResources = new TestSkinResourceProvider();
        var playbackDevice = new Mock<IWavePlayer>();
        var playbackEngine = new Mock<IPlaybackEngine>();
        playbackEngine.SetupGet(x => x.CurrentDevice).Returns(playbackDevice.Object);
        playbackEngine.SetupGet(x => x.EngineWaveFormat).Returns(new WaveFormat(44100, 16, 2));

        using var service = new GameplayAudioService(
            NullLogger<GameplayAudioService>.Instance,
            new SyncSessionContext(new AppSettings()),
            new AppSettings(),
            playbackEngine.Object,
            new AudioCacheManager(NullLogger<AudioCacheManager>.Instance),
            runtimeState,
            skinResources);
        service.SetContext(beatmapFolder, audioFilename: null);

        service.PrecacheMusicAndSkinInBackground();

        await WaitUntilAsync(() => skinResources.StableLookupCount > 0);
        Assert.False(service.TryGetCachedAudio("combobreak", out _));

        skinResources.SetStableResource("combobreak", CreatePcmWave());

        CachedAudio? cachedAudio = null;
        await WaitUntilAsync(() =>
        {
            if (!service.TryGetCachedAudio("combobreak", out var candidate) || candidate is not { Length: > 0 })
            {
                return false;
            }

            cachedAudio = candidate;
            return true;
        });

        Assert.NotNull(cachedAudio);
        Assert.True(cachedAudio.Length > 0);
    }

    public void Dispose()
    {
        Directory.Delete(_temporaryFolder, recursive: true);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    private static byte[] CreatePcmWave()
    {
        byte[] pcmData = [0xE8, 0x03, 0x18, 0xFC];
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + pcmData.Length);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)2);
            writer.Write(44100);
            writer.Write(44100 * 2 * sizeof(short));
            writer.Write((short)(2 * sizeof(short)));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(pcmData.Length);
            writer.Write(pcmData);
        }

        return stream.ToArray();
    }

    private sealed class TestPlaybackRuntimeState(string selectedSkinFolder) : IPlaybackRuntimeState
    {
        public bool AutoMode => false;

        public string SelectedSkinFolder { get; } = selectedSkinFolder;

        public event Action? SelectedSkinChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class TestSkinResourceProvider : ISkinResourceProvider
    {
        private readonly Dictionary<string, byte[]> _stableResources = new(StringComparer.OrdinalIgnoreCase);
        private int _stableLookupCount;

        public int StableLookupCount => Volatile.Read(ref _stableLookupCount);

        public event Action? ResourcesChanged;

        public bool TryGetSkinCatalog(string skinFolder, out IBeatmapResourceCatalog catalog)
        {
            catalog = null!;
            return false;
        }

        public bool TryGetLazerResource(string skinFolder, string key, out byte[] data)
        {
            data = null!;
            return false;
        }

        public bool TryGetStableResource(string key, out byte[] data)
        {
            var found = _stableResources.TryGetValue(key, out data!);
            Interlocked.Increment(ref _stableLookupCount);
            return found;
        }

        public void SetStableResource(string key, byte[] data)
        {
            _stableResources[key] = data;
            ResourcesChanged?.Invoke();
        }
    }
}
