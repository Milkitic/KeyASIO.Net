using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Tracks;
using Milki.Extensions.Configuration;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.Managers;

/// <summary>
/// 游戏状态管理器，负责处理 osu! 游戏状态变化
/// </summary>
public class GameStateManager : ViewModelBase
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(GameStateManager));

    private OsuMemoryStatus _osuStatus;
    private bool _result;
    private bool _previousSelectSongStatus = true;
    private int _pauseCount = 0;

    private readonly SelectSongTrack _selectSongTrack;

    public GameStateManager(SelectSongTrack selectSongTrack)
    {
        _selectSongTrack = selectSongTrack;
    }

    public OsuMemoryStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            var val = _osuStatus;
            if (SetField(ref _osuStatus, value))
            {
                OnStatusChanged(val, value);
            }
        }
    }

    public bool Result
    {
        get => _result;
        set => SetField(ref _result, value);
    }

    public bool PreviousSelectSongStatus
    {
        get => _previousSelectSongStatus;
        set => SetField(ref _previousSelectSongStatus, value);
    }

    public int PauseCount
    {
        get => _pauseCount;
        set => SetField(ref _pauseCount, value);
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;
    public event EventHandler? GameStartRequested;
    public event EventHandler? GameStopRequested;

    private async void OnStatusChanged(OsuMemoryStatus pre, OsuMemoryStatus cur)
    {
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(pre, cur));

        if (pre != OsuMemoryStatus.Playing &&
            cur == OsuMemoryStatus.Playing)
        {
            _selectSongTrack.StartLowPass(200, 800);
            _result = false;
            GameStartRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (pre == OsuMemoryStatus.Playing && cur == OsuMemoryStatus.ResultsScreen)
        {
            _result = true;
        }
        else if (pre != OsuMemoryStatus.NotRunning && cur == OsuMemoryStatus.NotRunning)
        {
            if (AppSettings.RealtimeOptions.EnableMusicFunctions)
            {
                _selectSongTrack.StopCurrentMusic(2000);
            }
        }
        else
        {
            _selectSongTrack.StartLowPass(200, 16000);
            _result = false;
            GameStopRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void HandlePlayTimePause(bool paused)
    {
        const int selectSongPauseThreshold = 20;

        if (paused && _previousSelectSongStatus)
        {
            _pauseCount++;
        }
        else if (!paused)
        {
            _pauseCount = 0;
        }

        var enableMusicFunctions = AppSettings.RealtimeOptions.EnableMusicFunctions;
        if (enableMusicFunctions && OsuStatus is OsuMemoryStatus.SongSelect or OsuMemoryStatus.SongSelectEdit or
                OsuMemoryStatus.MainMenu)
        {
            if (_pauseCount >= selectSongPauseThreshold && _previousSelectSongStatus)
            {
                _selectSongTrack.PauseCurrentMusic();
                _previousSelectSongStatus = false;
            }
            else if (_pauseCount < selectSongPauseThreshold && !_previousSelectSongStatus)
            {
                _selectSongTrack.RecoverCurrentMusic();
                _previousSelectSongStatus = true;
            }
        }
    }

    public void ResetPauseCount()
    {
        _pauseCount = 0;
    }
}

public class StatusChangedEventArgs : EventArgs
{
    public OsuMemoryStatus PreviousStatus { get; }
    public OsuMemoryStatus CurrentStatus { get; }

    public StatusChangedEventArgs(OsuMemoryStatus previousStatus, OsuMemoryStatus currentStatus)
    {
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
    }
}