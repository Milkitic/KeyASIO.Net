using System.Diagnostics;
using System.Text;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.OsuMemoryModels;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;

namespace KeyAsio.Shared.Realtime;

public class RealtimeSessionContext : ViewModelBase
{
    public Func<int, int, Task>? OnComboChanged;
    public Func<Mods, Mods, Task>? OnPlayModsChanged;
    public Func<int, int, bool, Task>? OnFetchedPlayTimeChanged;
    public Func<OsuMemoryStatus, OsuMemoryStatus, Task>? OnStatusChanged;
    public Func<BeatmapIdentifier, BeatmapIdentifier, Task>? OnBeatmapChanged;

    private readonly AppSettings _appSettings;
    private readonly Stopwatch _playTimeStopwatch = new();
    private readonly Stopwatch _uiUpdateStopwatch = Stopwatch.StartNew();
    private const long UiUpdateIntervalMs = 33; // ~30fps throttle

    public RealtimeSessionContext(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public bool IsStarted { get; set; }
    public bool IsReplay { get; set; }

    public int ProcessId
    {
        get;
        set => SetField(ref field, value);
    }

    public string? Username
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (!string.IsNullOrEmpty(value))
            {
                _appSettings.Logging.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
            }

            OnPropertyChanged();
        }
    }

    public Mods PlayMods
    {
        get;
        set
        {
            var val = field;
            if (SetField(ref field, value))
            {
                OnPlayModsChanged?.Invoke(val, value);
            }
        }
    }

    public int PlayTime
    {
        get;
        set
        {
            value += (int)_playTimeStopwatch.ElapsedMilliseconds;
            var oldValue = field;
            var changed = field != value;
            field = value;

            OnFetchedPlayTimeChanged?.Invoke(oldValue, value, !changed);

            if (_uiUpdateStopwatch.ElapsedMilliseconds >= UiUpdateIntervalMs)
            {
                _uiUpdateStopwatch.Restart();
                OnPropertyChanged();
            }
        }
    }

    public int BaseMemoryTime
    {
        get;
        set
        {
            bool changed = field != value;
            field = value;

            if (changed)
            {
                _playTimeStopwatch.Restart();
                OnPropertyChanged();
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
        get;
        set
        {
            var val = field;
            if (SetField(ref field, value))
            {
                OnComboChanged?.Invoke(val, value);
            }
        }
    }

    public int Score
    {
        get;
        set => SetField(ref field, value);
    }

    public OsuMemoryStatus OsuStatus
    {
        get;
        set
        {
            var val = field;
            if (SetField(ref field, value))
            {
                OnStatusChanged?.Invoke(val, value);
                OnPropertyChanged(nameof(SyncedStatusText));
            }
        }
    }

    public string SyncedStatusText => OsuStatus is OsuMemoryStatus.NotRunning or OsuMemoryStatus.Unknown
        ? "OFFLINE"
        : "SYNCED";

    public BeatmapIdentifier Beatmap
    {
        get;
        set
        {
            var val = field;
            if (SetField(ref field, value))
            {
                OnBeatmapChanged?.Invoke(val, value);
            }
        }
    }
}