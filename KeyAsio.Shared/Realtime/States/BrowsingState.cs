using Coosu.Beatmap;
using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class BrowsingState : IRealtimeState
{
    public Task EnterAsync(RealtimeModeManager ctx, OsuMemoryStatus from)
    {
        ctx.StartLowPass(200, 16000);
        ctx.SetResultFlag(false);
        ctx.Stop();
        return Task.CompletedTask;
    }

    public void Exit(RealtimeModeManager ctx, OsuMemoryStatus to)
    {
    }

    public void OnPlayTimeChanged(RealtimeModeManager ctx, int oldMs, int newMs, bool paused)
    {
    }

    public void OnBeatmapChanged(RealtimeModeManager ctx, BeatmapIdentifier beatmap)
    {
        if (beatmap == default)
        {
            return;
        }

        if (ctx.OsuStatus is not (
            OsuMemoryStatus.SongSelect or
            OsuMemoryStatus.SongSelectEdit or
            OsuMemoryStatus.MainMenu or
            OsuMemoryStatus.MultiplayerSongSelect)
           ) return;

        var coosu = OsuFile.ReadFromFile(beatmap.FilenameFull, k =>
        {
            k.IncludeSection("General");
            k.IncludeSection("Metadata");
        });

        var audioFilePath = coosu.General?.AudioFilename == null
            ? null
            : Path.Combine(beatmap.Folder, coosu.General.AudioFilename);

        if (audioFilePath == ctx.GetAudioFilePath())
        {
            return;
        }

        ctx.UpdateAudioPreviewContext(beatmap.Folder, audioFilePath);
        ctx.StopCurrentMusic(200);
        ctx.PlaySingleAudioPreview(coosu, audioFilePath, coosu.General.PreviewTime);
        ctx.ResetBrowsingPauseState();
    }

    public void OnModsChanged(RealtimeModeManager ctx, Mods oldMods, Mods newMods)
    {
    }
}