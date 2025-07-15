using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Tracks;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared.Realtime.Managers;

/// <summary>
/// 谱面管理器，负责处理谱面文件的加载和解析
/// </summary>
public class BeatmapManager : ViewModelBase
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(BeatmapManager));

    private BeatmapIdentifier _beatmap;
    private string? _folder;
    private string? _audioFilePath;

    private readonly SelectSongTrack _selectSongTrack;

    public BeatmapManager(SelectSongTrack selectSongTrack)
    {
        _selectSongTrack = selectSongTrack;
    }

    public BeatmapIdentifier Beatmap
    {
        get => _beatmap;
        set
        {
            if (SetField(ref _beatmap, value))
            {
                OnBeatmapChanged(value);
            }
        }
    }

    public string? Folder
    {
        get => _folder;
        set => SetField(ref _folder, value);
    }

    public string? AudioFilePath
    {
        get => _audioFilePath;
        set => SetField(ref _audioFilePath, value);
    }

    public OsuFile? OsuFile { get; set; }
    public string? AudioFilename { get; set; }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public event EventHandler<BeatmapChangedEventArgs>? BeatmapChanged;

    public async Task<BeatmapInitializationResult> InitializeNodeListsAsync(string folder, string diffFilename, Mods playMods)
    {
        var keyList = new List<PlayableNode>();
        var playbackList = new List<HitsoundNode>();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", Logger))
        {
            await osuDir.InitializeAsync(diffFilename, ignoreWaveFiles: AppSettings.RealtimeOptions.IgnoreBeatmapHitsound);
        }

        if (osuDir.OsuFiles.Count <= 0)
        {
            Logger.Warn($"There is no available beatmaps after scanning. " +
                              $"Directory: {folder}; File: {diffFilename}");
            return new BeatmapInitializationResult(keyList, playbackList, null, null);
        }

        var osuFile = osuDir.OsuFiles[0];
        OsuFile = osuFile;
        AudioFilename = osuFile.General?.AudioFilename;

        using var _ = DebugUtils.CreateTimer("InitAudio", Logger);
        var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
        await Task.Delay(100);

        var isNightcore = playMods != Mods.Unknown && (playMods & Mods.Nightcore) != 0;
        if (isNightcore || AppSettings.RealtimeOptions.ForceNightcoreBeats)
        {
            if (isNightcore)
            {
                Logger.Info("Current Mods:" + playMods);
            }

            var list = NightcoreTilingHelper.GetHitsoundNodes(osuFile, TimeSpan.Zero);
            hitsoundList.AddRange(list);
            hitsoundList = hitsoundList.OrderBy(k => k.Offset).ToList();
        }

        return new BeatmapInitializationResult(keyList, playbackList, osuFile, hitsoundList);
    }

    private void OnBeatmapChanged(BeatmapIdentifier beatmap)
    {
        BeatmapChanged?.Invoke(this, new BeatmapChangedEventArgs(beatmap));

        if (beatmap != default)
        {
            var coosu = OsuFile.ReadFromFile(beatmap.FilenameFull, k =>
            {
                k.IncludeSection("General");
                k.IncludeSection("Metadata");
            });
            var audioFilePath = coosu.General?.AudioFilename == null
                ? null
                : Path.Combine(beatmap.Folder, coosu.General.AudioFilename);
            if (audioFilePath == _audioFilePath)
            {
                return;
            }

            _folder = beatmap.Folder;
            _audioFilePath = audioFilePath;
            _selectSongTrack.StopCurrentMusic(200);
            _selectSongTrack.PlaySingleAudio(coosu, audioFilePath, coosu.General.PreviewTime);
        }
    }
}

public class BeatmapChangedEventArgs : EventArgs
{
    public BeatmapIdentifier Beatmap { get; }

    public BeatmapChangedEventArgs(BeatmapIdentifier beatmap)
    {
        Beatmap = beatmap;
    }
}

public class BeatmapInitializationResult
{
    public List<PlayableNode> KeyList { get; }
    public List<HitsoundNode> PlaybackList { get; }
    public OsuFile? OsuFile { get; }
    public List<HitsoundNode>? HitsoundList { get; }

    public BeatmapInitializationResult(List<PlayableNode> keyList, List<HitsoundNode> playbackList, OsuFile? osuFile, List<HitsoundNode>? hitsoundList)
    {
        KeyList = keyList;
        PlaybackList = playbackList;
        OsuFile = osuFile;
        HitsoundList = hitsoundList;
    }
}