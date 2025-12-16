using System.Runtime.CompilerServices;

namespace KeyAsio.Shared.OsuMemory;

public delegate void NotifyPropertyChangedEventHandler<in T>(T oldValue, T newValue);

public class MemoryReadObject
{
    public event NotifyPropertyChangedEventHandler<string?>? PlayerNameChanged;
    public event NotifyPropertyChangedEventHandler<int>? ComboChanged;
    public event NotifyPropertyChangedEventHandler<int>? ScoreChanged;
    public event NotifyPropertyChangedEventHandler<bool>? IsReplayChanged;
    public event NotifyPropertyChangedEventHandler<OsuMemoryStatus>? OsuStatusChanged;
    public event NotifyPropertyChangedEventHandler<int>? PlayingTimeChanged;
    public event NotifyPropertyChangedEventHandler<Mods>? ModsChanged;
    public event NotifyPropertyChangedEventHandler<int>? ProcessIdChanged;
    public event NotifyPropertyChangedEventHandler<BeatmapIdentifier>? BeatmapIdentifierChanged;

    public string? PlayerName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            PlayerNameChanged?.Invoke(old, value);
        }
    } // BanchoUser.UserName

    public int Combo
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            ComboChanged?.Invoke(old, value);
        }
    } // Player.(RulesetPlayData.Combo)

    public int Score
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            ScoreChanged?.Invoke(old, value);
        }
    } // Player.(RulesetPlayData.Score)

    public bool IsReplay
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            IsReplayChanged?.Invoke(old, value);
        }
    } // Player.IsReplay

    public OsuMemoryStatus OsuStatus
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            OsuStatusChanged?.Invoke(old, value);
        }
    } // GeneralData.OsuStatus

    public int PlayingTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            PlayingTimeChanged?.Invoke(old, value);
        }
    } // GeneralData.AudioTime

    public Mods Mods
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            ModsChanged?.Invoke(old, value);
        }
    } // GeneralData.Mods

    public int ProcessId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (field == value) return;
            var old = field;
            field = value;
            ProcessIdChanged?.Invoke(old, value);
        }
    }

    //public string? BeatmapFolder { get; set; } // CurrentBeatmap.FolderName
    //public string? BeatmapFileName { get; set; } // CurrentBeatmap.OsuFileName

    public BeatmapIdentifier BeatmapIdentifier
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (EqualityComparer<BeatmapIdentifier>.Default.Equals(field, value)) return;
            var old = field;
            field = value;
            BeatmapIdentifierChanged?.Invoke(old, value);
        }
    }
}