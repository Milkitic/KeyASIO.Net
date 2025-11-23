using Coosu.Beatmap;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Realtime.Tracks;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.Services;

public class BackgroundMusicManager
{
    private readonly SynchronizedMusicPlayer _synchronizedMusicPlayer;
    private readonly SongPreviewPlayer _songPreviewPlayer;
    private string? _previewFolder;
    private string? _previewAudioFilePath;
    private string? _mainTrackFolder;
    private string? _mainAudioFilename;
    private bool _previousSelectSongStatus = true;
    private int _pauseCount;
    private bool _firstStartInitialized;
    private bool _isResult;

    public BackgroundMusicManager(ILogger<SynchronizedMusicPlayer> mLogger, ILogger<SongPreviewPlayer> pLogger, AudioEngine audioEngine)
    {
        _synchronizedMusicPlayer = new SynchronizedMusicPlayer(mLogger, audioEngine);
        _songPreviewPlayer = new SongPreviewPlayer(pLogger, audioEngine);
    }

    public void StartLowPass(int lower, int upper)
    {
        _songPreviewPlayer.StartLowPass(lower, upper);
    }

    public void StopCurrentMusic(int fadeMs = 0)
    {
        _ = _songPreviewPlayer.StopCurrentMusic(fadeMs);
    }

    public void PauseCurrentMusic()
    {
        _ = _songPreviewPlayer.PauseCurrentMusic();
    }

    public void RecoverCurrentMusic()
    {
        _ = _songPreviewPlayer.RecoverCurrentMusic();
    }

    public void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime)
    {
        if (path is null) return;
        _ = _songPreviewPlayer.Play(osuFile, path, playTime);
    }

    public void SetSingleTrackPlayMods(Mods mods)
    {
        _synchronizedMusicPlayer.PlayMods = mods;
    }

    public void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs)
    {
        _synchronizedMusicPlayer.Offset = offset;
        _synchronizedMusicPlayer.LeadInMilliseconds = leadInMs;
    }

    public void SyncMainTrackAudio(CachedAudio sound, int positionMs)
    {
        _synchronizedMusicPlayer.SyncAudio(sound, positionMs);
    }

    public void ClearMainTrackAudio()
    {
        _synchronizedMusicPlayer.ClearAudio();
    }

    public void UpdatePreviewContext(string folder, string? audioFilePath)
    {
        _previewFolder = folder;
        _previewAudioFilePath = audioFilePath;
    }

    public string? GetPreviewAudioFilePath() => _previewAudioFilePath;

    public void UpdateMainTrackContext(string folder, string? audioFilename)
    {
        _mainTrackFolder = folder;
        _mainAudioFilename = audioFilename;
    }

    public string? GetMainTrackPath()
    {
        if (_mainTrackFolder == null || _mainAudioFilename == null) return null;
        return Path.Combine(_mainTrackFolder, _mainAudioFilename);
    }

    public string? GetMainTrackFolder() => _mainTrackFolder;
    public string? GetMainAudioFilename() => _mainAudioFilename;

    public void ResetPauseState()
    {
        _previousSelectSongStatus = true;
        _pauseCount = 0;
    }

    public void UpdatePauseCount(bool paused)
    {
        if (paused && _previousSelectSongStatus)
        {
            _pauseCount++;
        }
        else if (!paused)
        {
            _pauseCount = 0;
        }
    }

    public bool GetPreviousSelectSongStatus() => _previousSelectSongStatus;
    public void SetPreviousSelectSongStatus(bool value) => _previousSelectSongStatus = value;
    public int GetPauseCount() => _pauseCount;
    public void SetPauseCount(int value) => _pauseCount = value;

    public void SetResultFlag(bool value)
    {
        _isResult = value;
    }

    public bool IsResultFlag() => _isResult;

    public bool GetFirstStartInitialized() => _firstStartInitialized;
    public void SetFirstStartInitialized(bool value) => _firstStartInitialized = value;
}