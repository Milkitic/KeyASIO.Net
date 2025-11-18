using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio.Caching;

namespace KeyAsio.Shared.Models;

public class PlaybackInfo
{
    public PlaybackInfo(CachedAudio cachedAudio, HitsoundNode hitsoundNode)
    {
        CachedAudio = cachedAudio;
        HitsoundNode = hitsoundNode;
    }

    public HitsoundNode HitsoundNode { get; }

    public CachedAudio CachedAudio { get; }
}