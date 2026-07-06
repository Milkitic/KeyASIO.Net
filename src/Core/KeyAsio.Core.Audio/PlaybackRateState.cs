namespace KeyAsio.Core.Audio;

public readonly record struct PlaybackRateState(
    float Rate,
    bool PreservePitch,
    float PreservePitchCompensationMilliseconds = PlaybackRateState.DefaultPreservePitchCompensationMilliseconds)
{
    public const float DefaultPreservePitchCompensationMilliseconds = 5.0f;

    public static PlaybackRateState Normal { get; } = new(1.0f, false);
}
