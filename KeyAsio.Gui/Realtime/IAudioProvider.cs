using System.Collections.Generic;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Gui.Models;

namespace KeyAsio.Gui.Realtime;

public interface IAudioProvider
{
    IEnumerable<PlaybackInfo> GetPlaybackAudio(bool includeKey);
    IEnumerable<PlaybackInfo> GetKeyAudio(int keyIndex, int keyTotal);

    void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList,
        List<PlayableNode> playbackList, List<ControlNode> loopEffectList);
    void ResetNodes(int playTime);
}