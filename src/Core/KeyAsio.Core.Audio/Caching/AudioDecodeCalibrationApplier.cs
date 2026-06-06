using NAudio.Wave;

namespace KeyAsio.Core.Audio.Caching;

internal static class AudioDecodeCalibrationApplier
{
    public static AudioDecodeCorrection Apply(ReadOnlySpan<byte> decodedPcm, WaveFormat waveFormat,
        AudioDecodeCalibration calibration)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm || waveFormat.BitsPerSample != 16)
            return AudioDecodeCorrection.NotApplied;

        var frameSize = waveFormat.BlockAlign;
        if (frameSize <= 0 || decodedPcm.Length % frameSize != 0)
            return AudioDecodeCorrection.NotApplied;

        var offsetFrames = calibration.GetOffsetFrames(waveFormat.SampleRate);
        var durationDeltaFrames = calibration.GetDurationDeltaFrames(waveFormat.SampleRate);
        if (offsetFrames == 0 && durationDeltaFrames == 0)
            return AudioDecodeCorrection.NotApplied;

        var sourceFrames = decodedPcm.Length / frameSize;
        var targetFrames = Math.Max(0, sourceFrames - durationDeltaFrames);
        var outputLength = checked(targetFrames * frameSize);
        var owner = UnmanagedByteMemoryOwner.Allocate(outputLength);
        var output = owner.Memory.Span;
        output.Clear();

        var sourceStartFrames = Math.Max(0, offsetFrames);
        var destinationStartFrames = Math.Max(0, -offsetFrames);
        var framesAvailable = Math.Max(0, sourceFrames - sourceStartFrames);
        var framesWritable = Math.Max(0, targetFrames - destinationStartFrames);
        var framesToCopy = Math.Min(framesAvailable, framesWritable);

        if (framesToCopy > 0)
        {
            decodedPcm.Slice(sourceStartFrames * frameSize, framesToCopy * frameSize)
                .CopyTo(output.Slice(destinationStartFrames * frameSize));
        }

        return new AudioDecodeCorrection(owner, outputLength, offsetFrames, durationDeltaFrames);
    }
}

internal readonly record struct AudioDecodeCorrection(
    UnmanagedByteMemoryOwner? Owner,
    int Length,
    int OffsetFrames,
    int DurationDeltaFrames)
{
    public static AudioDecodeCorrection NotApplied => default;

    public bool Applied => Owner != null;
}
