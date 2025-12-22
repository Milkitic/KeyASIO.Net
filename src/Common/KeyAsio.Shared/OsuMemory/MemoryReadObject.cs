using System.Runtime.CompilerServices;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Events;

namespace KeyAsio.Shared.OsuMemory;

public class MemoryReadObject
{
    public event ValueChangedEventHandler<string?>? PlayerNameChanged;
    public event ValueChangedEventHandler<int>? ComboChanged;
    public event ValueChangedEventHandler<int>? ScoreChanged;
    public event ValueChangedEventHandler<bool>? IsReplayChanged;
    public event ValueChangedEventHandler<OsuMemoryStatus>? OsuStatusChanged;
    public event ValueChangedEventHandler<int>? PlayingTimeChanged;
    public event ValueChangedEventHandler<Mods>? ModsChanged;
    public event ValueChangedEventHandler<int>? ProcessIdChanged;
    public event ValueChangedEventHandler<BeatmapIdentifier>? BeatmapIdentifierChanged;

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