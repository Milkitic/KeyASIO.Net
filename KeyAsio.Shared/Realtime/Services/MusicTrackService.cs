using Coosu.Beatmap;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Realtime.Tracks;

namespace KeyAsio.Shared.Realtime.Services;

public class MusicTrackService
{
    private readonly SingleSynchronousTrack _singleSynchronousTrack;
    private readonly SelectSongTrack _selectSongTrack;
    private string? _previewFolder;
    private string? _previewAudioFilePath;
    private string? _mainTrackFolder;
    private string? _mainAudioFilename;
    private bool _previousSelectSongStatus = true;
    private int _pauseCount;

    public MusicTrackService(AudioEngine audioEngine)
    {
        _singleSynchronousTrack = new SingleSynchronousTrack(audioEngine);
        _selectSongTrack = new SelectSongTrack(audioEngine);
    }

    public void StartLowPass(int lower, int upper)
    {
        _selectSongTrack.StartLowPass(lower, upper);
    }

    public void StopCurrentMusic(int fadeMs = 0)
    {
        _ = _selectSongTrack.StopCurrentMusic(fadeMs);
    }

    public void PauseCurrentMusic()
    {
        _ = _selectSongTrack.PauseCurrentMusic();
    }

    public void RecoverCurrentMusic()
    {
        _ = _selectSongTrack.RecoverCurrentMusic();
    }

    public void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime)
    {
        if (path is null) return;
        _ = _selectSongTrack.PlaySingleAudio(osuFile, path, playTime);
    }

    public void SetSingleTrackPlayMods(Mods mods)
    {
        _singleSynchronousTrack.PlayMods = mods;
    }

    public void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs)
    {
        _singleSynchronousTrack.Offset = offset;
        _singleSynchronousTrack.LeadInMilliseconds = leadInMs;
    }

    public void SyncMainTrackAudio(CachedAudio sound, int positionMs)
    {
        _singleSynchronousTrack.SyncAudio(sound, positionMs);
    }

    public void ClearMainTrackAudio()
    {
        _singleSynchronousTrack.ClearAudio();
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
}