using System.ComponentModel;
using PropertyChanged;

namespace KeyAsio.Shared.OsuMemory;

public delegate void NotifyPropertyChangedEventHandler<in T>(T oldValue, T newValue);

public partial class MemoryReadObject : INotifyPropertyChanged
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

    [OnChangedMethod(nameof(OnPlayerNameChanged))]
    public string? PlayerName { get; set; } // BanchoUser.UserName

    [OnChangedMethod(nameof(OnComboChanged))]
    public int Combo { get; set; } // Player.(RulesetPlayData.Combo)

    [OnChangedMethod(nameof(OnScoreChanged))]
    public int Score { get; set; } // Player.(RulesetPlayData.Score)

    [OnChangedMethod(nameof(OnIsReplayChanged))]
    public bool IsReplay { get; set; } // Player.IsReplay

    [OnChangedMethod(nameof(OnOsuStatusChanged))]
    public OsuMemoryStatus OsuStatus { get; set; } // GeneralData.OsuStatus

    [OnChangedMethod(nameof(OnPlayingTimeChanged))]
    public int PlayingTime { get; set; } // GeneralData.AudioTime

    [OnChangedMethod(nameof(OnModsChanged))]
    public Mods Mods { get; set; } // GeneralData.Mods

    [OnChangedMethod(nameof(OnProcessIdChanged))]
    public int ProcessId { get; set; }

    //public string? BeatmapFolder { get; set; } // CurrentBeatmap.FolderName
    //public string? BeatmapFileName { get; set; } // CurrentBeatmap.OsuFileName

    [OnChangedMethod(nameof(OnBeatmapIdentifierChanged))]
    public BeatmapIdentifier BeatmapIdentifier { get; set; }

    private void OnPlayerNameChanged(string? oldValue, string? newValue)
    {
        PlayerNameChanged?.Invoke(oldValue, newValue);
    }

    private void OnComboChanged(int oldValue, int newValue)
    {
        ComboChanged?.Invoke(oldValue, newValue);
    }

    private void OnScoreChanged(int oldValue, int newValue)
    {
        ScoreChanged?.Invoke(oldValue, newValue);
    }

    private void OnIsReplayChanged(bool oldValue, bool newValue)
    {
        IsReplayChanged?.Invoke(oldValue, newValue);
    }

    private void OnOsuStatusChanged(OsuMemoryStatus oldValue, OsuMemoryStatus newValue)
    {
        OsuStatusChanged?.Invoke(oldValue, newValue);
    }

    private void OnPlayingTimeChanged(int oldValue, int newValue)
    {
        PlayingTimeChanged?.Invoke(oldValue, newValue);
    }

    private void OnModsChanged(Mods oldValue, Mods newValue)
    {
        ModsChanged?.Invoke(oldValue, newValue);
    }

    private void OnProcessIdChanged(int oldValue, int newValue)
    {
        ProcessIdChanged?.Invoke(oldValue, newValue);
    }

    private void OnBeatmapIdentifierChanged(BeatmapIdentifier oldValue, BeatmapIdentifier newValue)
    {
        BeatmapIdentifierChanged?.Invoke(oldValue, newValue);
    }
}