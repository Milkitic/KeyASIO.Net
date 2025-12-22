using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Shared.Models;

namespace KeyAsio.Shared.Sync;

public interface IHitsoundSequencer
{
    void ProcessAutoPlay(List<PlaybackInfo> buffer, bool processHitQueueAsAuto);
    void ProcessInteraction(List<PlaybackInfo> buffer, int keyIndex, int keyTotal);

    void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList, List<HitsoundNode> playbackList);
    void SeekTo(int playTime);
}