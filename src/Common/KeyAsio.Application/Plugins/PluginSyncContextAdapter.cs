using KeyAsio.Plugins.Contracts;
using KeyAsio.Sync;
using KeyAsio.Sync.Sources;

namespace KeyAsio.Application.Plugins;

internal sealed class PluginSyncContextAdapter : ISyncContext
{
    private readonly SyncSessionContext _context;
    private BeatmapIdentifier _cachedIdentifier;
    private SyncBeatmapInfo? _cachedBeatmap;

    public PluginSyncContextAdapter(SyncSessionContext context)
    {
        _context = context;
    }

    public int PlayTime => _context.PlayTime;
    public double BeatmapOffset => _context.BeatmapOffset;
    public bool IsStarted => _context.IsStarted;
    public SyncOsuStatus OsuStatus => (SyncOsuStatus)_context.OsuStatus;
    public long LastUpdateTimestamp => _context.LastUpdateTimestamp;
    public int PlayMods => (int)_context.PlayMods;
    public SyncStatistics Statistics => _context.Statistics;
    public SyncHitErrors HitErrors => _context.HitErrors;
    public bool IsAudioPaused => _context.IsAudioPaused;

    public SyncBeatmapInfo? Beatmap
    {
        get
        {
            var current = _context.Beatmap;
            if (current == _cachedIdentifier)
            {
                return _cachedBeatmap;
            }

            _cachedIdentifier = current;
            _cachedBeatmap = current.Folder is null ? null : ToPluginBeatmap(current);
            return _cachedBeatmap;
        }
    }

    internal static SyncBeatmapInfo ToPluginBeatmap(BeatmapIdentifier beatmap) =>
        new()
        {
            Folder = beatmap.Folder,
            Filename = beatmap.Filename
        };
}
