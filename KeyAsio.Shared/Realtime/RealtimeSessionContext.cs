using System.Diagnostics;
using System.Text;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeSessionContext : ViewModelBase
{
    public Func<int, int, ValueTask>? OnComboChanged;
    public Func<Mods, Mods, ValueTask>? OnPlayModsChanged;
    public Func<int, int, bool, ValueTask>? OnFetchedPlayTimeChanged;
    public Func<OsuMemoryStatus, OsuMemoryStatus, ValueTask>? OnStatusChanged;
    public Func<BeatmapIdentifier, BeatmapIdentifier, ValueTask>? OnBeatmapChanged;

    private readonly YamlAppSettings _appSettings;
    private readonly Stopwatch _playTimeStopwatch = new();

    private int _playTime;
    private string? _username;
    private Mods _playMods;
    private int _baseMemoryTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;

    public RealtimeSessionContext(YamlAppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public bool IsStarted { get; set; }
    public bool IsReplay { get; set; }

    public string? Username
    {
        get => _username;
        set
        {
            if (_username == value) return;
            _username = value;
            if (!string.IsNullOrEmpty(value))
            {
                _appSettings.Logging.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
            }

            OnPropertyChanged();
        }
    }

    public Mods PlayMods
    {
        get => _playMods;
        set
        {
            var val = _playMods;
            if (SetField(ref _playMods, value))
            {
                OnPlayModsChanged?.Invoke(val, value);
            }
        }
    }

    public int PlayTime
    {
        get => _playTime;
        set
        {
            value += (int)_playTimeStopwatch.ElapsedMilliseconds;
            var val = _playTime;
            if (SetField(ref _playTime, value))
            {
                OnFetchedPlayTimeChanged?.Invoke(val, value, false);
            }
            else
            {
                OnFetchedPlayTimeChanged?.Invoke(val, value, true);
            }
        }
    }

    public int BaseMemoryTime
    {
        get => _baseMemoryTime;
        set
        {
            if (SetField(ref _baseMemoryTime, value))
            {
                _playTimeStopwatch.Restart();
            }
            else
            {
                _playTimeStopwatch.Reset();
            }

            PlayTime = value;
        }
    }

    public int Combo
    {
        get => _combo;
        set
        {
            var val = _combo;
            if (SetField(ref _combo, value))
            {
                OnComboChanged?.Invoke(val, value);
            }
        }
    }

    public int Score
    {
        get => _score;
        set => SetField(ref _score, value);
    }

    public OsuMemoryStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            var val = _osuStatus;
            if (SetField(ref _osuStatus, value))
            {
                OnStatusChanged?.Invoke(val, value);
            }
        }
    }

    public BeatmapIdentifier Beatmap
    {
        get => _beatmap;
        set
        {
            var val = _beatmap;
            if (SetField(ref _beatmap, value))
            {
                OnBeatmapChanged?.Invoke(val, value);
            }
        }
    }
}