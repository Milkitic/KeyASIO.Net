using Coosu.Beatmap.Extensions.Playback;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;

namespace KeyAsio.Shared.Models;

public class PlaybackInfo
{
    public PlaybackInfo(CachedSound? cachedSound, HitsoundNode hitsoundNode)
    {
        CachedSound = cachedSound;
        HitsoundNode = hitsoundNode;
    }

    public HitsoundNode HitsoundNode { get; }

    public CachedSound? CachedSound { get; }
}