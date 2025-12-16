using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.Services;

public class BeatmapHitsoundLoader
{
    private readonly ILogger<BeatmapHitsoundLoader> _logger;
    private readonly AppSettings _appSettings;
    private readonly GameplayAudioService _gameplayAudioService;

    private readonly List<PlayableNode> _keyList = new();
    private readonly List<HitsoundNode> _playbackList = new();
    private int _nextCachingTime;

    public BeatmapHitsoundLoader(ILogger<BeatmapHitsoundLoader> logger, AppSettings appSettings,
        GameplayAudioService gameplayAudioService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _gameplayAudioService = gameplayAudioService;
    }

    public IReadOnlyList<HitsoundNode> PlaybackList => _playbackList;
    public List<PlayableNode> KeyList => _keyList;

    public async Task<OsuFile?> InitializeNodeListsAsync(string folder, string diffFilename,
        IHitsoundSequencer hitsoundSequencer, Mods playMods)
    {
        _keyList.Clear();
        _playbackList.Clear();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", _logger))
        {
            await osuDir.InitializeAsync(diffFilename,
                ignoreWaveFiles: _appSettings.Sync.Filters.DisableBeatmapHitsounds);
        }

        if (osuDir.OsuFiles.Count <= 0)
        {
            _logger.LogWarning("There is no available beatmaps after scanning. Directory: {Folder}; File: {Filename}",
                folder, diffFilename);
            return null;
        }

        var osuFile = osuDir.OsuFiles[0];

        using var _ = DebugUtils.CreateTimer("InitAudio", _logger);
        var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
        await Task.Delay(100);

        var isNightcore = playMods != Mods.Unknown && (playMods & Mods.Nightcore) != 0;
        if (isNightcore || _appSettings.Sync.Playback.NightcoreBeats)
        {
            if (isNightcore)
            {
                _logger.LogInformation("Current Mods: {PlayMods}", playMods);
            }

            var list = NightcoreTilingHelper.GetHitsoundNodes(osuFile, TimeSpan.Zero);
            hitsoundList.AddRange(list);
            hitsoundList = hitsoundList.OrderBy(k => k.Offset).ToList();
        }

        hitsoundSequencer.FillAudioList(hitsoundList, _keyList, _playbackList);
        return osuFile;
    }

    public void ResetNodes(IHitsoundSequencer hitsoundSequencer, int playTime)
    {
        hitsoundSequencer.SeekTo(playTime);
        _gameplayAudioService.PrecacheHitsoundsRangeInBackground(0, 13000, _keyList);
        _gameplayAudioService.PrecacheHitsoundsRangeInBackground(0, 13000, _playbackList);
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
        _gameplayAudioService.PrecacheHitsoundsRangeInBackground(startTime, endTime, playableNodes);
    }
}