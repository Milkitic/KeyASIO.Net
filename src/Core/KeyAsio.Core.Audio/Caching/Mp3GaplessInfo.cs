using System.Buffers.Binary;
using KeyAsio.Core.Audio.Utils;

namespace KeyAsio.Core.Audio.Caching;

internal readonly record struct Mp3GaplessInfo(int SampleRate, int StartSkipSamples, int EndDiscardSamples)
{
    private const int Mp3DecoderDelay = 529;

    public int TotalDiscardSamples => StartSkipSamples + EndDiscardSamples;

    public static bool TryRead(ReadOnlySpan<byte> data, out Mp3GaplessInfo info)
    {
        info = default;

        if (!MpegAudioFrameScanner.TryFindFirstFrame(
                data,
                out var frame,
                out var offset,
                requireLayer3: true))
        {
            return false;
        }

        if (offset + frame.FrameLength > data.Length)
            return false;

        var frameData = data.Slice(offset, frame.FrameLength);
        return TryReadXingOrInfo(frameData, frame, out info) ||
               TryReadVbri(frameData, frame, out info);
    }

    private static bool TryReadXingOrInfo(ReadOnlySpan<byte> frameData, MpegAudioFrameHeader frame,
        out Mp3GaplessInfo info)
    {
        info = default;

        var xingOffset = GetXingOffset(frame);
        if (xingOffset < 0 || xingOffset + 8 > frameData.Length)
            return false;

        var marker = frameData.Slice(xingOffset, 4);
        if (!marker.SequenceEqual("Xing"u8) && !marker.SequenceEqual("Info"u8))
            return false;

        var cursor = xingOffset + 4;
        var flags = BinaryPrimitives.ReadInt32BigEndian(frameData.Slice(cursor, 4));
        cursor += 4;

        if ((flags & 0x1) != 0)
            cursor += 4;
        if ((flags & 0x2) != 0)
            cursor += 4;
        if ((flags & 0x4) != 0)
            cursor += 100;
        if ((flags & 0x8) != 0)
            cursor += 4;

        if (cursor + 24 > frameData.Length)
            return false;

        var encoder = frameData.Slice(cursor, 9);
        if (!encoder.StartsWith("LAME"u8) &&
            !encoder.StartsWith("Lavf"u8) &&
            !encoder.StartsWith("Lavc"u8))
        {
            return false;
        }

        cursor += 9;
        cursor += 1; // Info tag revision + VBR method
        cursor += 1; // lowpass filter
        cursor += 4; // replaygain peak
        cursor += 2; // radio replaygain
        cursor += 2; // audiophile replaygain
        cursor += 1; // encoding flags + ATH type
        cursor += 1; // bitrate

        if (cursor + 3 > frameData.Length)
            return false;

        var delayAndPadding =
            (frameData[cursor] << 16) |
            (frameData[cursor + 1] << 8) |
            frameData[cursor + 2];

        var encoderDelay = delayAndPadding >> 12;
        var encoderPadding = delayAndPadding & 0xFFF;
        if (encoderDelay <= 0 && encoderPadding <= 0)
            return false;

        var startSkip = encoderDelay + Mp3DecoderDelay;
        var endDiscard = Math.Max(0, encoderPadding - Mp3DecoderDelay);
        if (startSkip <= 0 && endDiscard <= 0)
            return false;

        info = new Mp3GaplessInfo(frame.SampleRate, startSkip, endDiscard);
        return true;
    }

    private static bool TryReadVbri(ReadOnlySpan<byte> frameData, MpegAudioFrameHeader frame, out Mp3GaplessInfo info)
    {
        info = default;

        const int vbriOffset = 4 + 32;
        if (vbriOffset + 8 > frameData.Length ||
            !frameData.Slice(vbriOffset, 4).SequenceEqual("VBRI"u8))
            return false;

        var encoderDelay = BinaryPrimitives.ReadUInt16BigEndian(frameData.Slice(vbriOffset + 6, 2));
        var startSkip = Math.Max(0, encoderDelay - frame.SamplesPerFrame);
        if (startSkip <= 0)
            return false;

        info = new Mp3GaplessInfo(frame.SampleRate, startSkip, 0);
        return true;
    }

    private static int GetXingOffset(MpegAudioFrameHeader frame)
    {
        return frame.Version == MpegVersion.Version1
            ? frame.Channels == 1 ? 4 + 17 : 4 + 32
            : frame.Channels == 1
                ? 4 + 9
                : 4 + 17;
    }
}
