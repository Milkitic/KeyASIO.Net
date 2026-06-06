namespace KeyAsio.Core.Audio.Caching;

public sealed class AudioDecodeCalibration
{
    public required string SourceHash { get; init; }

    public int SampleRate { get; init; }

    public int OffsetFrames { get; init; }

    public int DurationDeltaFrames { get; init; }

    public double Correlation { get; init; }

    public string? Name { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    internal int GetOffsetFrames(int targetSampleRate)
    {
        return RescaleFrames(OffsetFrames, targetSampleRate);
    }

    internal int GetDurationDeltaFrames(int targetSampleRate)
    {
        return RescaleFrames(DurationDeltaFrames, targetSampleRate);
    }

    private int RescaleFrames(int frames, int targetSampleRate)
    {
        if (SampleRate <= 0 || targetSampleRate <= 0 || SampleRate == targetSampleRate)
            return frames;

        return (int)Math.Round(frames * (double)targetSampleRate / SampleRate, MidpointRounding.AwayFromZero);
    }
}
