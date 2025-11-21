using System.Diagnostics;
using System.Text;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeProperties : ViewModelBase
{
    public Func<int, int, ValueTask>? OnComboChanged;
    public Func<Mods, Mods, ValueTask>? OnPlayModsChanged;
    public Func<int, int, bool, ValueTask>? OnFetchedPlayTimeChanged;
    public Func<OsuMemoryStatus, OsuMemoryStatus, ValueTask>? OnStatusChanged;
    public Func<BeatmapIdentifier, BeatmapIdentifier, ValueTask>? OnBeatmapChanged;

    private readonly AppSettings _appSettings;
    private readonly Stopwatch _playTimeStopwatch = new();

    private string? _username;
    private Mods _playMods;
    private int _baseMemoryTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;

    public RealtimeProperties(AppSettings appSettings)
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
                _appSettings.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
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
        get
        {
            var offset = _appSettings.RealtimeOptions.RealtimeModeAudioOffset;
            var interpolated = _playTimeStopwatch.ElapsedMilliseconds;
            return _baseMemoryTime + offset + (int)interpolated;
        }
    }

    public int BaseMemoryTime
    {
        get => _baseMemoryTime;
        set
        {
            var playTime = PlayTime;
            var val = _baseMemoryTime;
            if (SetField(ref _baseMemoryTime, value))
            {
                _playTimeStopwatch.Restart();
            }
            else
            {
                _playTimeStopwatch.Reset();
            }

            OnPropertyChanged(nameof(PlayTime));
            OnFetchedPlayTimeChanged?.Invoke(playTime, PlayTime, val == value);
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