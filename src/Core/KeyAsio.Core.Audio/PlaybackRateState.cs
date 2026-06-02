namespace KeyAsio.Core.Audio;

public readonly record struct PlaybackRateState(float Rate, bool PreservePitch)
{
    public static PlaybackRateState Normal { get; } = new(1.0f, false);
}
