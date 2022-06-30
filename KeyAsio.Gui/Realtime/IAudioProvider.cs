using System.Collections.Generic;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Gui.Models;

namespace KeyAsio.Gui.Realtime;

public interface IAudioProvider
{
    IEnumerable<PlaybackInfo> GetPlaybackAudio(bool includeKey);
    IEnumerable<PlaybackInfo> GetKeyAudio(int keyIndex, int keyTotal);

    void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList, List<HitsoundNode> playbackList);
    void ResetNodes(int playTime);
}