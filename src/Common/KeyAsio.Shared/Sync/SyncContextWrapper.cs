using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.OsuMemory;

namespace KeyAsio.Shared.Sync;

public class SyncContextWrapper : ISyncContext
{
    private readonly SyncSessionContext _context;

    private BeatmapIdentifier _cachedIdentifier;
    private SyncBeatmapInfo? _cachedInfo;

    public SyncContextWrapper(SyncSessionContext context)
    {
        _context = context;
    }

    public int PlayTime => _context.PlayTime;
    public bool IsStarted => _context.IsStarted;

    public SyncOsuStatus OsuStatus => (SyncOsuStatus)_context.OsuStatus;

    public long LastUpdateTimestamp => _context.LastUpdateTimestamp;

    public int PlayMods => (int)_context.PlayMods;

    public SyncBeatmapInfo? Beatmap
    {
        get
        {
            var current = _context.Beatmap;
            if (current == _cachedIdentifier) return _cachedInfo;

            _cachedIdentifier = current;
            _cachedInfo = current.Folder == null
                ? null
                : new SyncBeatmapInfo
                {
                    Folder = current.Folder,
                    Filename = current.Filename
                };

            return _cachedInfo;
        }
    }
}