using Coosu.Beatmap;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public interface IRealtimeContext
{
    int PlayTime { get; }
    BeatmapIdentifier Beatmap { get; }
    Mods PlayMods { get; }
    bool IsStarted { get; }
    OsuMemoryStatus OsuStatus { get; }
    OsuFile? OsuFile { get; }
    int Score { get; }
    bool IsReplay { get; }
    IAudioProvider CurrentAudioProvider { get; }
    Task StartAsync(string beatmapFilenameFull, string beatmapFilename);
    void Stop();
    void FillPlaybackAudio(List<PlaybackInfo> buffer, bool isAuto);
}