using KeyAsio.Plugins.Contracts;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Models;
using KeyAsio.Sync.Sources;
using KeyAsio.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace KeyAsio.UnitTests;

public sealed class GameSyncSourceCoordinatorTests
{
    [Fact]
    public void ApplySnapshot_UpdatesStatusBeforeComboChanged()
    {
        var context = new SyncSessionContext(new AppSettings())
        {
            IsStarted = true,
            OsuStatus = OsuMemoryStatus.Playing,
            Statistics = SyncStatistics.Empty,
            Combo = 42
        };

        var source = new FakeGameSyncSource(new GameSyncSnapshot
        {
            ClientType = GameClientType.Lazer,
            Status = OsuMemoryStatus.Playing,
            Combo = 42,
            Statistics = SyncStatistics.Empty
        });
        var coordinator = new GameSyncSourceCoordinator(
            context,
            [source],
            NullLogger<GameSyncSourceCoordinator>.Instance);

        OsuMemoryStatus? statusSeenOnComboChanged = null;
        context.OnComboChanged = (_, _) =>
        {
            statusSeenOnComboChanged = context.OsuStatus;
            return Task.CompletedTask;
        };

        coordinator.Start();

        source.Publish(new GameSyncSnapshot
        {
            ClientType = GameClientType.Lazer,
            Status = OsuMemoryStatus.ResultsScreen,
            Score = 123456,
            Combo = 0,
            Statistics = new SyncStatistics(Perfect: 0, Great: 12, Good: 0, Ok: 0, Meh: 0, Miss: 0)
        });

        Assert.Equal(OsuMemoryStatus.ResultsScreen, statusSeenOnComboChanged);
    }

    [Fact]
    public void ApplySnapshot_UpdatesStatisticsBeforeComboChanged()
    {
        var context = new SyncSessionContext(new AppSettings())
        {
            IsStarted = true,
            OsuStatus = OsuMemoryStatus.Playing,
            Statistics = SyncStatistics.Empty,
            Combo = 42
        };

        var source = new FakeGameSyncSource(new GameSyncSnapshot
        {
            ClientType = GameClientType.Lazer,
            Status = OsuMemoryStatus.Playing,
            Combo = 42,
            Statistics = SyncStatistics.Empty
        });
        var coordinator = new GameSyncSourceCoordinator(
            context,
            [source],
            NullLogger<GameSyncSourceCoordinator>.Instance);

        SyncStatistics? statisticsSeenOnComboChanged = null;
        context.OnComboChanged = (_, _) =>
        {
            statisticsSeenOnComboChanged = context.Statistics;
            return Task.CompletedTask;
        };

        coordinator.Start();

        var statistics = new SyncStatistics(Perfect: 0, Great: 12, Good: 0, Ok: 0, Meh: 0, Miss: 1);
        source.Publish(new GameSyncSnapshot
        {
            ClientType = GameClientType.Lazer,
            Status = OsuMemoryStatus.Playing,
            Score = 123456,
            Combo = 0,
            Statistics = statistics
        });

        Assert.Equal(statistics, statisticsSeenOnComboChanged);
    }

    [Fact]
    public void ApplySnapshot_ExposesBeatmapOffsetToPlugins()
    {
        var context = new SyncSessionContext(new AppSettings());
        var source = new FakeGameSyncSource(new GameSyncSnapshot
        {
            ClientType = GameClientType.Lazer,
            Status = OsuMemoryStatus.Playing,
            BeatmapOffset = 12.3
        });
        var coordinator = new GameSyncSourceCoordinator(
            context,
            [source],
            NullLogger<GameSyncSourceCoordinator>.Instance);
        var pluginContext = new SyncContextWrapper(context);

        coordinator.Start();

        Assert.Equal(12.3, pluginContext.BeatmapOffset);

        source.Publish(new GameSyncSnapshot
        {
            ClientType = GameClientType.Lazer,
            Status = OsuMemoryStatus.Playing,
            BeatmapOffset = -4.7
        });

        Assert.Equal(-4.7, pluginContext.BeatmapOffset);
    }

    private sealed class FakeGameSyncSource : IGameSyncSource
    {
        public FakeGameSyncSource(GameSyncSnapshot initialSnapshot)
        {
            CurrentSnapshot = initialSnapshot;
        }

        public string Name => "fake";
        public GameClientType ClientType => CurrentSnapshot.ClientType;
        public int Priority => 100;
        public bool IsAvailable { get; private set; }
        public GameSyncSnapshot CurrentSnapshot { get; private set; }

        public event Action<IGameSyncSource, bool>? AvailabilityChanged;
        public event Action<IGameSyncSource, GameSyncSnapshot>? SnapshotReceived;

        public void Start()
        {
            IsAvailable = true;
            AvailabilityChanged?.Invoke(this, true);
        }

        public Task StopAsync()
        {
            IsAvailable = false;
            AvailabilityChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        public void Publish(GameSyncSnapshot snapshot)
        {
            CurrentSnapshot = snapshot;
            SnapshotReceived?.Invoke(this, snapshot);
        }
    }
}
