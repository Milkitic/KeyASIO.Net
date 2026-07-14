using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Services;
using KeyAsio.Sync.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class AudioDeviceOperationCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_StartsBeforeCommittingSettings()
    {
        var previous = Device("previous");
        var requested = Device("requested");
        var settings = Settings(previous, 44100);
        var engine = new PlaybackEngineHarness(previous, 44100);
        var persistence = new Mock<IAppSettingsPersistence>();
        var audioCache = new Mock<IGameplayAudioCache>();
        var events = engine.Events;
        persistence.Setup(x => x.Save()).Callback(() => events.Add("save"));
        audioCache.Setup(x => x.ClearCaches()).Callback(() => events.Add("clear"));

        using var coordinator = CreateCoordinator(settings, persistence, engine, audioCache);
        var result = await coordinator.ApplyAsync(requested, 96000, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(requested, settings.Audio.PlaybackDevice);
        Assert.Equal(96000, settings.Audio.SampleRate);
        Assert.Equal(["stop:previous", "start:requested", "save", "clear"], events);
        Assert.Equal(requested, result.ActiveDevice);
    }

    [Fact]
    public async Task ApplyAsync_WhenStartFails_RestoresDeviceAndSettings()
    {
        var previous = Device("previous");
        var requested = Device("requested");
        var settings = Settings(previous, 44100);
        var engine = new PlaybackEngineHarness(previous, 44100)
        {
            StartFailure = device => device == requested ? new InvalidOperationException("cannot start") : null
        };
        var persistence = new Mock<IAppSettingsPersistence>();
        var audioCache = new Mock<IGameplayAudioCache>();

        using var coordinator = CreateCoordinator(settings, persistence, engine, audioCache);
        var result = await coordinator.ApplyAsync(requested, 96000, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasRolledBack);
        Assert.Equal(previous, settings.Audio.PlaybackDevice);
        Assert.Equal(44100, settings.Audio.SampleRate);
        Assert.Equal(previous, result.ActiveDevice);
        Assert.Equal(["stop:previous", "start:requested", "start:previous"], engine.Events);
        persistence.Verify(x => x.Save(), Times.Never);
        audioCache.Verify(x => x.ClearCaches(), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_WhenCommitFails_RestoresPersistedAndRuntimeState()
    {
        var previous = Device("previous");
        var requested = Device("requested");
        var settings = Settings(previous, 48000);
        var engine = new PlaybackEngineHarness(previous, 48000);
        var persistence = new Mock<IAppSettingsPersistence>();
        var saveAttempt = 0;
        persistence.Setup(x => x.Save()).Callback(() =>
        {
            if (Interlocked.Increment(ref saveAttempt) == 1)
            {
                throw new IOException("write failed");
            }
        });
        var audioCache = new Mock<IGameplayAudioCache>();

        using var coordinator = CreateCoordinator(settings, persistence, engine, audioCache);
        var result = await coordinator.ApplyAsync(requested, 96000, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasRolledBack);
        Assert.Equal(previous, settings.Audio.PlaybackDevice);
        Assert.Equal(48000, settings.Audio.SampleRate);
        Assert.Equal(previous, result.ActiveDevice);
        persistence.Verify(x => x.Save(), Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyAsync_WhenStoppingFailsTransiently_RetriesThenApplies()
    {
        var previous = Device("previous");
        var requested = Device("requested");
        var settings = Settings(previous, 44100);
        var engine = new PlaybackEngineHarness(previous, 44100)
        {
            StopFailuresRemaining = 2
        };
        var persistence = new Mock<IAppSettingsPersistence>();
        var audioCache = new Mock<IGameplayAudioCache>();

        using var coordinator = CreateCoordinator(settings, persistence, engine, audioCache);
        var result = await coordinator.ApplyAsync(requested, 48000, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(requested, result.ActiveDevice);
        Assert.Equal(
            ["stop:previous", "stop:previous", "stop:previous", "start:requested"],
            engine.Events);
        persistence.Verify(x => x.Save(), Times.Once);
        audioCache.Verify(x => x.ClearCaches(), Times.Once);
    }

    private static AudioDeviceOperationCoordinator CreateCoordinator(
        AppSettings settings,
        Mock<IAppSettingsPersistence> persistence,
        PlaybackEngineHarness engine,
        Mock<IGameplayAudioCache> audioCache) =>
        new(
            settings,
            persistence.Object,
            engine.Engine.Object,
            audioCache.Object,
            Mock.Of<ILogger<AudioDeviceOperationCoordinator>>());

    private static AppSettings Settings(DeviceDescription device, int sampleRate) => new()
    {
        Audio = new AppSettingsAudio
        {
            PlaybackDevice = device,
            SampleRate = sampleRate
        }
    };

    private static DeviceDescription Device(string name) => new()
    {
        WavePlayerType = WavePlayerType.WASAPI,
        DeviceId = name,
        FriendlyName = name
    };

    private sealed class PlaybackEngineHarness
    {
        private readonly IWavePlayer _device = Mock.Of<IWavePlayer>();
        private IWavePlayer? _currentDevice;
        private DeviceDescription? _description;
        private WaveFormat _sourceFormat;

        public PlaybackEngineHarness(DeviceDescription? initialDevice, int initialSampleRate)
        {
            _currentDevice = initialDevice is null ? null : _device;
            _description = initialDevice;
            _sourceFormat = new WaveFormat(initialSampleRate, 2);

            Engine.SetupGet(x => x.CurrentDevice).Returns(() => _currentDevice);
            Engine.SetupGet(x => x.CurrentDeviceDescription).Returns(() => _description);
            Engine.SetupGet(x => x.SourceWaveFormat).Returns(() => _sourceFormat);
            Engine.Setup(x => x.StopDevice()).Callback(() =>
            {
                Events.Add($"stop:{_description?.DeviceId}");
                if (StopFailuresRemaining > 0)
                {
                    StopFailuresRemaining--;
                    throw new IOException("transient stop failure");
                }

                _currentDevice = null;
                _description = null;
            });
            Engine.Setup(x => x.StartDevice(It.IsAny<DeviceDescription?>(), It.IsAny<WaveFormat?>()))
                .Callback((DeviceDescription? device, WaveFormat? format) =>
                {
                    Events.Add($"start:{device?.DeviceId}");
                    var failure = StartFailure?.Invoke(device);
                    if (failure is not null)
                    {
                        throw failure;
                    }

                    _currentDevice = device is null ? null : _device;
                    _description = device;
                    if (format is not null)
                    {
                        _sourceFormat = format;
                    }
                });
        }

        public Mock<IPlaybackEngine> Engine { get; } = new();
        public List<string> Events { get; } = [];
        public Func<DeviceDescription?, Exception?>? StartFailure { get; init; }
        public int StopFailuresRemaining { get; set; }
    }
}
