using Coosu.Beatmap;
using KeyAsio.Audio.Caching;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.OsuMemory;

namespace KeyAsio.Shared.Plugins;

public interface IMusicManagerPlugin : IPlugin
{
    void StartLowPass(int fadeMilliseconds, int targetFrequency);
    void StopCurrentMusic(int fadeMs = 0);
    void PauseCurrentMusic();
    void RecoverCurrentMusic();
    void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime);
    void SetSingleTrackPlayMods(Mods mods);
    void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs);
    void SyncMainTrackAudio(CachedAudio sound, int positionMs);
    void ClearMainTrackAudio();

    // State Management
    void ResetPauseState();
    void UpdatePauseCount(bool paused);
    bool GetPreviousSelectSongStatus();
    void SetPreviousSelectSongStatus(bool value);
    int GetPauseCount();
    void SetPauseCount(int value);
    bool GetFirstStartInitialized();
    void SetFirstStartInitialized(bool value);
}
