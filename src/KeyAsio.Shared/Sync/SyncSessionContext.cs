using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Utils;

namespace KeyAsio.Shared.Sync;

public class SyncSessionContext
{
    public Func<int, int, Task>? OnComboChanged;
    public Func<Mods, Mods, Task>? OnPlayModsChanged;
    public Func<int, int, bool, Task>? OnFetchedPlayTimeChanged;
    public Func<OsuMemoryStatus, OsuMemoryStatus, Task>? OnStatusChanged;
    public Func<BeatmapIdentifier, BeatmapIdentifier, Task>? OnBeatmapChanged;

    private readonly AppSettings _appSettings;
    private long _startTick;
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;

    public SyncSessionContext(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public bool IsStarted { get; set; }
    public bool IsReplay { get; set; }

    public int ProcessId { get; set; }

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
        }
    }

    public Mods PlayMods
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;
            OnPlayModsChanged?.Invoke(oldValue, value);
        }
    }

    public int PlayTime
    {
        get;
        set
        {
            var currentTick = Stopwatch.GetTimestamp();
            LastUpdateTimestamp = currentTick;
            if (_startTick != 0)
            {
                value += (int)((currentTick - _startTick) * TickToMs);
            }

            var oldValue = field;
            var changed = field != value;
            field = value;

            OnFetchedPlayTimeChanged?.Invoke(oldValue, value, !changed);
        }
    }

    public int BaseMemoryTime
    {
        get;
        set
        {
            bool changed = field != value;
            field = value;

            _startTick = changed ? Stopwatch.GetTimestamp() : 0;

            PlayTime = value;
        }
    }

    public int Combo
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;
            OnComboChanged?.Invoke(oldValue, value);
        }
    }

    public int Score { get; set; }

    public OsuMemoryStatus OsuStatus
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;
            OnStatusChanged?.Invoke(oldValue, value);
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
            if (field == value) return;
            var oldValue = field;
            field = value;
            OnBeatmapChanged?.Invoke(oldValue, value);
        }
    }

    public long LastUpdateTimestamp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }
}