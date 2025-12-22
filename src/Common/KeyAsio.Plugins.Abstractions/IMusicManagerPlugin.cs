using Coosu.Beatmap;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Plugins.Abstractions.OsuMemory;

namespace KeyAsio.Plugins.Abstractions;

public interface IMusicManagerPlugin : IPlugin
{
    event EventHandler? OptionStateChanged;

    string OptionName { get; }
    string OptionTag { get; }
    int OptionPriority { get; }
    bool CanEnableOption { get; }

    void StartLowPass(int fadeMilliseconds, int targetFrequency);
    void StopCurrentMusic(int fadeMs = 0);
    void PauseCurrentMusic();
    void RecoverCurrentMusic();
    void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime);
    void SetSingleTrackPlayMods(Mods mods);
    void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs);
    void SyncMainTrackAudio(CachedAudio sound, int positionMs);
    void ClearMainTrackAudio();
}