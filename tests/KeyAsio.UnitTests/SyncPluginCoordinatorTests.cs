using KeyAsio.Application.Plugins;
using KeyAsio.Configuration;
using KeyAsio.Plugins.Contracts;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync;
using KeyAsio.Sync.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KeyAsio.UnitTests;

public sealed class SyncPluginCoordinatorTests
{
    [Fact]
    public void HandleTick_ExposesReadOnlyAdaptedContextAndReusesBeatmapProjection()
    {
        var session = new SyncSessionContext(new AppSettings())
        {
            BeatmapOffset = 12.5,
            Beatmap = new BeatmapIdentifier("Songs/1", "map.osu"),
            OsuStatus = OsuMemoryStatus.Playing
        };
        ISyncContext? receivedContext = null;
        var plugin = new Mock<ISyncPlugin>();
        plugin.Setup(item => item.OnTick(It.IsAny<ISyncContext>(), 4))
            .Callback<ISyncContext, int>((context, _) => receivedContext = context);

        var manager = CreatePluginManager([plugin.Object], []);
        var coordinator = CreateCoordinator(manager.Object, session);

        coordinator.Start();
        var blocked = coordinator.HandleTick(4, OsuMemoryStatus.Playing);

        Assert.False(blocked);
        Assert.NotNull(receivedContext);
        Assert.Equal(12.5, receivedContext.BeatmapOffset);
        Assert.Equal(SyncOsuStatus.Playing, receivedContext.OsuStatus);
        Assert.Same(receivedContext.Beatmap, receivedContext.Beatmap);
        plugin.Verify(item => item.OnSyncStart(), Times.Once);
    }

    [Fact]
    public void HandleStateEnter_HonorsBlockAllAndStopsLowerPriorityHandlers()
    {
        var highPriority = new Mock<IGameStateHandler>();
        highPriority.SetupGet(handler => handler.Priority).Returns(100);
        highPriority.Setup(handler => handler.HandleEnter(It.IsAny<ISyncContext>()))
            .Returns(HandleResult.BlockAll);

        var lowPriority = new Mock<IGameStateHandler>();
        lowPriority.SetupGet(handler => handler.Priority).Returns(0);

        var manager = CreatePluginManager([], [highPriority.Object, lowPriority.Object]);
        var coordinator = CreateCoordinator(manager.Object, new SyncSessionContext(new AppSettings()));

        var blocked = coordinator.HandleStateEnter(OsuMemoryStatus.Playing);

        Assert.True(blocked);
        highPriority.Verify(handler => handler.HandleEnter(It.IsAny<ISyncContext>()), Times.Once);
        lowPriority.Verify(handler => handler.HandleEnter(It.IsAny<ISyncContext>()), Times.Never);
    }

    [Fact]
    public void HandleStateExit_ContinuesAfterFaultingHandler()
    {
        var faultingHandler = new Mock<IGameStateHandler>();
        faultingHandler.Setup(handler => handler.HandleExit(It.IsAny<ISyncContext>()))
            .Throws<InvalidOperationException>();

        var blockingHandler = new Mock<IGameStateHandler>();
        blockingHandler.Setup(handler => handler.HandleExit(It.IsAny<ISyncContext>()))
            .Returns(HandleResult.BlockBaseLogic);

        var manager = CreatePluginManager([], [faultingHandler.Object, blockingHandler.Object]);
        var coordinator = CreateCoordinator(manager.Object, new SyncSessionContext(new AppSettings()));

        Assert.True(coordinator.HandleStateExit(OsuMemoryStatus.Playing));
        blockingHandler.Verify(handler => handler.HandleExit(It.IsAny<ISyncContext>()), Times.Once);
    }

    private static SyncPluginCoordinator CreateCoordinator(
        IPluginManager manager,
        SyncSessionContext session) =>
        new(manager, NullLogger<SyncPluginCoordinator>.Instance, session);

    private static Mock<IPluginManager> CreatePluginManager(
        IReadOnlyList<IPlugin> plugins,
        IReadOnlyList<IGameStateHandler> handlers)
    {
        var manager = new Mock<IPluginManager>();
        manager.Setup(item => item.GetAllPlugins()).Returns(plugins);
        manager.Setup(item => item.GetActiveHandlers(It.IsAny<SyncOsuStatus>())).Returns(handlers);
        return manager;
    }
}
