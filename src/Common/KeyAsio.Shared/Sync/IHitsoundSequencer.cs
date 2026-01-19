using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Models;

namespace KeyAsio.Shared.Sync;

public interface IHitsoundSequencer
{
    void ProcessAutoPlay(List<PlaybackInfo> buffer, bool processHitQueueAsAuto);
    void ProcessInteraction(List<PlaybackInfo> buffer, int keyIndex, int keyTotal);

    void FillAudioList(IReadOnlyList<PlaybackEvent> nodeList, List<SampleEvent> keyList, List<PlaybackEvent> playbackList);
    void SeekTo(int playTime);
}