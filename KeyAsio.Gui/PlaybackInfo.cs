using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;

namespace KeyAsio.Gui;

public class PlaybackInfo
{
    public PlaybackInfo(CachedSound cachedSound, float volume, float balance)
    {
        CachedSound = cachedSound;
        Volume = volume;
        Balance = balance;
    }

    public CachedSound CachedSound { get; }
    public float Volume { get; }
    public float Balance { get; }
}