using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Shared.Models;

namespace KeyAsio.Shared.Realtime;

public interface IHitsoundSequencer
{
    void FillPlaybackAudio(List<PlaybackInfo> buffer, bool includeKey);
    void FillKeyAudio(List<PlaybackInfo> buffer, int keyIndex, int keyTotal);

    void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList, List<HitsoundNode> playbackList);
    void ResetNodes(int playTime);
}