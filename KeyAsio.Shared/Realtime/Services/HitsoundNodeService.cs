using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Utils;

namespace KeyAsio.Shared.Realtime.Services;

public class HitsoundNodeService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(HitsoundNodeService));

    private readonly RealtimeModeManager _ctx;
    private readonly AudioCacheService _audioCacheService;

    private readonly List<PlayableNode> _keyList = new();
    private readonly List<HitsoundNode> _playbackList = new();
    private int _nextCachingTime;

    public HitsoundNodeService(RealtimeModeManager ctx, AudioCacheService audioCacheService)
    {
        _ctx = ctx;
        _audioCacheService = audioCacheService;
    }

    public IReadOnlyList<HitsoundNode> PlaybackList => _playbackList;
    public List<PlayableNode> KeyList => _keyList;

    public async Task InitializeNodeListsAsync(string folder, string diffFilename, IAudioProvider audioProvider)
    {
        _keyList.Clear();
        _playbackList.Clear();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", Logger))
        {
            await osuDir.InitializeAsync(diffFilename,
                ignoreWaveFiles: _ctx.AppSettings.RealtimeOptions.IgnoreBeatmapHitsound);
        }

        if (osuDir.OsuFiles.Count <= 0)
        {
            Logger.Warn($"There is no available beatmaps after scanning. " +
                        $"Directory: {folder}; File: {diffFilename}");
            return;
        }

        var osuFile = osuDir.OsuFiles[0];
        _ctx.OsuFile = osuFile;
        _ctx.AudioFilename = osuFile.General?.AudioFilename;

        using var _ = DebugUtils.CreateTimer("InitAudio", Logger);
        var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
        await Task.Delay(100);

        var isNightcore = _ctx.PlayMods != Mods.Unknown && (_ctx.PlayMods & Mods.Nightcore) != 0;
        if (isNightcore || _ctx.AppSettings.RealtimeOptions.ForceNightcoreBeats)
        {
            if (isNightcore)
            {
                Logger.Info("Current Mods:" + _ctx.PlayMods);
            }

            var list = NightcoreTilingHelper.GetHitsoundNodes(osuFile, TimeSpan.Zero);
            hitsoundList.AddRange(list);
            hitsoundList = hitsoundList.OrderBy(k => k.Offset).ToList();
        }

        audioProvider.FillAudioList(hitsoundList, _keyList, _playbackList);
    }

    public void ResetNodes(IAudioProvider audioProvider, int playTime)
    {
        audioProvider.ResetNodes(playTime);
        _audioCacheService.PrecacheHitsoundsRangeInBackground(0, 13000, _keyList);
        _audioCacheService.PrecacheHitsoundsRangeInBackground(0, 13000, _playbackList);
        _nextCachingTime = 10000;
    }

    public void AdvanceCachingWindow(int newMs)
    {
        if (newMs > _nextCachingTime)
        {
            AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _keyList);
            AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _playbackList);
            _nextCachingTime += 10000;
        }
    }

    private void AddAudioCacheInBackground(int startTime, int endTime, IEnumerable<HitsoundNode> playableNodes)
    {
        _audioCacheService.PrecacheHitsoundsRangeInBackground(startTime, endTime, playableNodes);
    }
}