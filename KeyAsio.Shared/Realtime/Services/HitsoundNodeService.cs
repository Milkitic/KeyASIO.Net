using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.Services;

public class HitsoundNodeService
{
    private readonly ILogger<HitsoundNodeService> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioCacheService _audioCacheService;

    private readonly List<PlayableNode> _keyList = new();
    private readonly List<HitsoundNode> _playbackList = new();
    private int _nextCachingTime;

    public HitsoundNodeService(ILogger<HitsoundNodeService> logger, AppSettings appSettings,
        AudioCacheService audioCacheService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheService = audioCacheService;
    }

    public IReadOnlyList<HitsoundNode> PlaybackList => _playbackList;
    public List<PlayableNode> KeyList => _keyList;

    public async Task<OsuFile?> InitializeNodeListsAsync(string folder, string diffFilename,
        IAudioProvider audioProvider, Mods playMods)
    {
        _keyList.Clear();
        _playbackList.Clear();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", _logger))
        {
            await osuDir.InitializeAsync(diffFilename,
                ignoreWaveFiles: _appSettings.RealtimeOptions.IgnoreBeatmapHitsound);
        }

        if (osuDir.OsuFiles.Count <= 0)
        {
            Logger.Warn($"There is no available beatmaps after scanning. " +
                        $"Directory: {folder}; File: {diffFilename}");
            return null;
        }

        var osuFile = osuDir.OsuFiles[0];

        using var _ = DebugUtils.CreateTimer("InitAudio", _logger);
        var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
        await Task.Delay(100);

        var isNightcore = playMods != Mods.Unknown && (playMods & Mods.Nightcore) != 0;
        if (isNightcore || _appSettings.RealtimeOptions.ForceNightcoreBeats)
        {
            if (isNightcore)
            {
                Logger.Info("Current Mods:" + playMods);
            }

            var list = NightcoreTilingHelper.GetHitsoundNodes(osuFile, TimeSpan.Zero);
            hitsoundList.AddRange(list);
            hitsoundList = hitsoundList.OrderBy(k => k.Offset).ToList();
        }

        audioProvider.FillAudioList(hitsoundList, _keyList, _playbackList);
        return osuFile;
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