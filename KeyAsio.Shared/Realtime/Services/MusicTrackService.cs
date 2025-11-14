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

    public void PlaySingleAudioPreview(OsuFile osuFile, string path, int playTime)
    {
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
}