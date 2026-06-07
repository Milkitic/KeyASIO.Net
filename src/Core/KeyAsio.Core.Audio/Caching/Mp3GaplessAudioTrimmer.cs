using NAudio.Wave;

namespace KeyAsio.Core.Audio.Caching;

internal static class Mp3GaplessAudioTrimmer
{
    public static Mp3GaplessTrimResult Apply(ReadOnlySpan<byte> decodedPcm, WaveFormat waveFormat,
        Mp3GaplessInfo gaplessInfo)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm || waveFormat.BitsPerSample != 16)
            return Mp3GaplessTrimResult.NotApplied;

        var frameSize = waveFormat.BlockAlign;
        if (frameSize <= 0 || decodedPcm.Length % frameSize != 0)
            return Mp3GaplessTrimResult.NotApplied;

        var startSkipFrames = Rescale(gaplessInfo.StartSkipSamples, gaplessInfo.SampleRate, waveFormat.SampleRate);
        var totalDiscardFrames = Rescale(gaplessInfo.TotalDiscardSamples, gaplessInfo.SampleRate, waveFormat.SampleRate);
        if (startSkipFrames == 0 && totalDiscardFrames == 0)
            return Mp3GaplessTrimResult.NotApplied;

        var sourceFrames = decodedPcm.Length / frameSize;
        var targetFrames = Math.Max(0, sourceFrames - totalDiscardFrames);
        var outputLength = checked(targetFrames * frameSize);
        var owner = UnmanagedByteMemoryOwner.Allocate(outputLength);

        var sourceStartFrames = Math.Min(sourceFrames, Math.Max(0, startSkipFrames));
        var framesAvailable = Math.Max(0, sourceFrames - sourceStartFrames);
        var framesToCopy = Math.Min(framesAvailable, targetFrames);

        if (framesToCopy > 0)
        {
            decodedPcm.Slice(sourceStartFrames * frameSize, framesToCopy * frameSize)
                .CopyTo(owner.Memory.Span);
        }

        return new Mp3GaplessTrimResult(owner, outputLength, startSkipFrames, totalDiscardFrames);
    }

    private static int Rescale(int frames, int sourceSampleRate, int targetSampleRate)
    {
        if (frames == 0 || sourceSampleRate == targetSampleRate)
            return frames;

        return (int)Math.Round(frames * (double)targetSampleRate / sourceSampleRate);
    }
}

internal readonly record struct Mp3GaplessTrimResult(
    UnmanagedByteMemoryOwner? Owner,
    int Length,
    int StartSkipFrames,
    int TotalDiscardFrames)
{
    public static Mp3GaplessTrimResult NotApplied => default;

    public bool Applied => Owner != null;
}
