using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;

namespace KeyAsio.Gui;

public class SharedViewModel : ViewModelBase
{
    public event OsuListenerManager.OnStatusChangedEvt? OnStatusChanged;
    public event OsuListenerManager.OnPlayingTimeChangedEvt? OnPlayTimeChanged;
    public event OsuListenerManager.OnBeatmapChangedEvt? OnBeatmapChanged;

    private OsuListenerManager.OsuStatus _osuStatus;
    private int _playTime;
    private Beatmap? _beatmap;

    private SharedViewModel()
    {
    }

    public static SharedViewModel Instance { get; } = new();

    public int PlayTime
    {
        get => _playTime;
        set
        {
            if (value == _playTime) return;
            _playTime = value;
            OnPlayTimeChanged?.Invoke(value);
            OnPropertyChanged();
        }
    }

    public OsuListenerManager.OsuStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            if (value == _osuStatus) return;
            var val = _osuStatus;
            _osuStatus = value;
            OnStatusChanged?.Invoke(val, value);
            OnPropertyChanged();
        }
    }

    public Beatmap? Beatmap
    {
        get => _beatmap;
        set
        {
            if (Equals(value, _beatmap)) return;
            _beatmap = value;
            OnBeatmapChanged?.Invoke(value);
            OnPropertyChanged();
        }
    }

    public OsuListenerManager? OsuListenerManager { get; set; }
}