using KeyAsio.Plugins.Abstractions;

namespace KeyAsio.Shared.Sync;

public class SyncContextWrapper : ISyncContext
{
    private readonly SyncSessionContext _context;

    public SyncContextWrapper(SyncSessionContext context)
    {
        _context = context;
    }

    public int PlayTime => _context.PlayTime;
    public bool IsPaused => !_context.IsStarted;
    public bool IsStarted => _context.IsStarted;

    public SyncOsuStatus OsuStatus => (SyncOsuStatus)_context.OsuStatus;

    public long LastUpdateTimestamp => _context.LastUpdateTimestamp;

    public int PlayMods => (int)_context.PlayMods;

    public SyncBeatmapInfo? Beatmap => _context.Beatmap.Folder == null 
        ? null 
        : new SyncBeatmapInfo 
        { 
            Folder = _context.Beatmap.Folder, 
            Filename = _context.Beatmap.Filename 
        };
}
