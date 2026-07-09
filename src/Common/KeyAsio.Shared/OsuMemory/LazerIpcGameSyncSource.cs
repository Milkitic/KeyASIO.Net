using KeyAsio.Core.OsuAudio.Hitsounds;
using KeyAsio.LazerProtocol;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Sync;

namespace KeyAsio.Shared.OsuMemory;

public sealed class LazerIpcGameSyncSource : IGameSyncSource
{
    private readonly LazerIpcBridge _lazerIpcBridge;
    private readonly GameSyncSnapshot _snapshot;
    private readonly LazerIpcFrame _frame = new();
    private readonly object frameLock = new();
    private bool _eventsBound;
    private bool _connected;
    private bool _hasTimingFrame;
    private bool _hasEventFrame;
    private IBeatmapResourceCatalog? _resourceCatalog;
    private LazerSkinInfo[]? _lastPublishedSkinInfos;
    private string? _lastPublishedUserDataDirectory;
    private string? _lastPublishedExeDirectory;

    public event Action<LazerSkinInfo[]?, string?, string?>? LazerSkinContextReceived;

    public LazerIpcGameSyncSource(LazerIpcBridge lazerIpcBridge)
    {
        _lazerIpcBridge = lazerIpcBridge;
        _snapshot = GameSyncSnapshot.NotRunning(ClientType);
        CurrentSnapshot = _snapshot;
    }

    public string Name => "osu!lazer IPC";
    public GameClientType ClientType => GameClientType.Lazer;
    public int Priority => 100;
    public bool IsAvailable => _connected;
    public GameSyncSnapshot CurrentSnapshot { get; private set; }

    public event Action<IGameSyncSource, bool>? AvailabilityChanged;
    public event Action<IGameSyncSource, GameSyncSnapshot>? SnapshotReceived;

    public void Start()
    {
        BindEvents();
        _lazerIpcBridge.Start();
    }

    public async Task StopAsync()
    {
        await _lazerIpcBridge.StopAsync();

        bool availabilityChanged;
        lock (frameLock)
        {
            ResetFrameStateLocked();
            availabilityChanged = SetAvailabilityLocked(false);
        }

        if (availabilityChanged)
            AvailabilityChanged?.Invoke(this, false);
    }

    private void BindEvents()
    {
        if (_eventsBound) return;

        _lazerIpcBridge.ChannelConnectionChanged += OnChannelConnectionChanged;
        _lazerIpcBridge.FrameReceived += OnFrameReceived;
        _eventsBound = true;
    }

    private void OnChannelConnectionChanged(LazerIpcChannel channel, bool oldValue, bool newValue)
    {
        bool availabilityChanged;
        bool isAvailable;

        lock (frameLock)
        {
            if (!newValue)
            {
                ResetFrameStateLocked();
            }

            isAvailable = CanBeAvailableLocked();
            availabilityChanged = SetAvailabilityLocked(isAvailable);
        }

        if (availabilityChanged)
            AvailabilityChanged?.Invoke(this, isAvailable);
    }

    private void OnFrameReceived(LazerIpcChannel channel, LazerDeltaFrame deltaFrame)
    {
        bool availabilityChanged;
        bool isAvailable;
        LazerSkinInfo[]? skinInfosToPublish = null;
        string? userDataDirectoryToPublish = null;
        string? exeDirectoryToPublish = null;
        bool skinContextChanged = false;

        lock (frameLock)
        {
            switch (channel)
            {
                case LazerIpcChannel.Timing:
                    _hasTimingFrame = true;
                    break;

                case LazerIpcChannel.Events:
                    _hasEventFrame = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }

            ApplyFrameLocked(deltaFrame);
            isAvailable = CanBeAvailableLocked();
            availabilityChanged = SetAvailabilityLocked(isAvailable);

            // Detect changes to lazer skin context.
            if (!ReferenceEquals(_frame.SkinInfos, _lastPublishedSkinInfos))
            {
                _lastPublishedSkinInfos = _frame.SkinInfos;
                skinInfosToPublish = _frame.SkinInfos;
                skinContextChanged = true;
            }

            if (_frame.UserDataDirectory != _lastPublishedUserDataDirectory)
            {
                _lastPublishedUserDataDirectory = _frame.UserDataDirectory;
                userDataDirectoryToPublish = _frame.UserDataDirectory;
                skinContextChanged = true;
            }

            if (_frame.ExeDirectory != _lastPublishedExeDirectory)
            {
                _lastPublishedExeDirectory = _frame.ExeDirectory;
                exeDirectoryToPublish = _frame.ExeDirectory;
                skinContextChanged = true;
            }
        }

        if (availabilityChanged)
            AvailabilityChanged?.Invoke(this, isAvailable);

        if (isAvailable)
            SnapshotReceived?.Invoke(this, _snapshot);

        if (skinContextChanged)
        {
            LazerSkinContextReceived?.Invoke(
                skinInfosToPublish,
                userDataDirectoryToPublish,
                exeDirectoryToPublish);
        }
    }

    private void ApplyFrameLocked(LazerDeltaFrame deltaFrame)
    {
        var beatmapChanged = deltaFrame.HasField(LazerFieldKind.BeatmapFolder) ||
                             deltaFrame.HasField(LazerFieldKind.BeatmapFilename);
        var beatmapFilesChanged = deltaFrame.HasField(LazerFieldKind.BeatmapFiles);

        if (beatmapChanged && !beatmapFilesChanged)
        {
            _resourceCatalog = null;
            _frame.ClearBeatmapFiles();
        }

        _frame.Apply(deltaFrame);
        var frame = _frame;

        var status = Enum.IsDefined(typeof(OsuMemoryStatus), frame.Status)
            ? (OsuMemoryStatus)frame.Status
            : OsuMemoryStatus.Unknown;

        if (beatmapFilesChanged && frame.BeatmapFiles.Length > 0)
        {
            var resourceCatalog = BeatmapResourceCatalog.FromMappings(
                frame.BeatmapFiles.Select(file => new BeatmapResource(file.Name, file.Path)),
                frame.BeatmapFolder,
                CreateCatalogCacheKey(frame));

            if (!resourceCatalog.IsEmpty)
            {
                _resourceCatalog = resourceCatalog;
            }
        }

        var beatmap = !string.IsNullOrWhiteSpace(frame.BeatmapFolder) &&
                      !string.IsNullOrWhiteSpace(frame.BeatmapFilename)
            ? new BeatmapIdentifier(frame.BeatmapFolder, frame.BeatmapFilename)
            : default;

        var snapshot = _snapshot;
        snapshot.ProcessId = frame.ProcessId;
        snapshot.Username = frame.Username;
        snapshot.PlayMods = (Mods)frame.Mods;
        snapshot.IsReplay = frame.IsReplay;
        snapshot.Score = frame.Score;
        snapshot.Combo = frame.Combo;
        snapshot.Statistics = frame.Statistics;
        snapshot.HitErrors = new SyncHitErrors(frame.HitErrorIndex, frame.HitErrors);
        snapshot.Beatmap = beatmap;
        snapshot.BeatmapResourceCatalog = _resourceCatalog;
        snapshot.PlayTime = frame.PlayTime;
        snapshot.Status = status;
    }

    private void ResetFrameStateLocked()
    {
        _hasTimingFrame = false;
        _hasEventFrame = false;
        _resourceCatalog = null;
        _frame.Reset();
        _snapshot.ResetToNotRunning(ClientType);
        CurrentSnapshot = _snapshot;

        _lastPublishedSkinInfos = null;
        _lastPublishedUserDataDirectory = null;
        _lastPublishedExeDirectory = null;
    }

    private bool CanBeAvailableLocked()
        => _lazerIpcBridge.IsTimingConnected &&
           _lazerIpcBridge.IsEventsConnected &&
           _hasTimingFrame &&
           _hasEventFrame;

    private bool SetAvailabilityLocked(bool isAvailable)
    {
        if (_connected == isAvailable) return false;

        _connected = isAvailable;
        return true;
    }

    private static string CreateCatalogCacheKey(LazerIpcFrame frame)
    {
        var beatmapFilename = frame.BeatmapFilename;
        var beatmapPath = string.IsNullOrWhiteSpace(beatmapFilename)
            ? null
            : frame.BeatmapFiles.FirstOrDefault(file =>
                string.Equals(BeatmapResourceCatalog.NormalizeName(file.Name),
                    BeatmapResourceCatalog.NormalizeName(beatmapFilename), StringComparison.OrdinalIgnoreCase))?.Path;

        return $"lazer:{frame.BeatmapFolder}:{beatmapFilename}:{beatmapPath}:{frame.BeatmapFiles.Length}";
    }
}
