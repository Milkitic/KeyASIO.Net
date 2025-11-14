using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio.Caching;

namespace KeyAsio.Shared.Models;

public class PlaybackInfo
{
    public PlaybackInfo(CachedAudio cachedSound, HitsoundNode hitsoundNode)
    {
        CachedSound = cachedSound;
        HitsoundNode = hitsoundNode;
    }

    public HitsoundNode HitsoundNode { get; }

    public CachedAudio CachedSound { get; }
}