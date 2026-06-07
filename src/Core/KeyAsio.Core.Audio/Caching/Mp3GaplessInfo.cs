using System.Buffers.Binary;
using System.Text;

namespace KeyAsio.Core.Audio.Caching;

internal readonly record struct Mp3GaplessInfo(int SampleRate, int StartSkipSamples, int EndDiscardSamples)
{
    private const int Mp3DecoderDelay = 529;

    public int TotalDiscardSamples => StartSkipSamples + EndDiscardSamples;

    public static bool TryRead(ReadOnlySpan<byte> data, out Mp3GaplessInfo info)
    {
        info = default;

        var offset = SkipId3v2(data);
        if (!TryReadFrameHeader(data, offset, out var frame))
            return false;

        if (frame.Layer != MpegLayer.Layer3 || frame.FrameLength <= 0 ||
            offset + frame.FrameLength > data.Length)
        {
            return false;
        }

        var frameData = data.Slice(offset, frame.FrameLength);
        return TryReadXingOrInfo(frameData, frame, out info) ||
               TryReadVbri(frameData, frame, out info);
    }

    private static bool TryReadXingOrInfo(ReadOnlySpan<byte> frameData, Mp3FrameHeader frame,
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

        var encoder = Encoding.ASCII.GetString(frameData.Slice(cursor, 9));
        if (!encoder.StartsWith("LAME", StringComparison.Ordinal) &&
            !encoder.StartsWith("Lavf", StringComparison.Ordinal) &&
            !encoder.StartsWith("Lavc", StringComparison.Ordinal))
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

    private static bool TryReadVbri(ReadOnlySpan<byte> frameData, Mp3FrameHeader frame, out Mp3GaplessInfo info)
    {
        info = default;

        const int vbriOffset = 4 + 32;
        if (vbriOffset + 8 > frameData.Length ||
            !frameData.Slice(vbriOffset, 4).SequenceEqual("VBRI"u8))
        {
            return false;
        }

        var encoderDelay = BinaryPrimitives.ReadUInt16BigEndian(frameData.Slice(vbriOffset + 6, 2));
        var startSkip = Math.Max(0, encoderDelay - frame.SamplesPerFrame);
        if (startSkip <= 0)
            return false;

        info = new Mp3GaplessInfo(frame.SampleRate, startSkip, 0);
        return true;
    }

    private static int SkipId3v2(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10 || !data.Slice(0, 3).SequenceEqual("ID3"u8))
            return 0;

        var size =
            ((data[6] & 0x7F) << 21) |
            ((data[7] & 0x7F) << 14) |
            ((data[8] & 0x7F) << 7) |
            (data[9] & 0x7F);

        var footerSize = (data[5] & 0x10) != 0 ? 10 : 0;
        return Math.Min(data.Length, 10 + size + footerSize);
    }

    private static int GetXingOffset(Mp3FrameHeader frame)
    {
        return frame.Version == MpegVersion.Version1
            ? frame.Channels == 1 ? 4 + 17 : 4 + 32
            : frame.Channels == 1 ? 4 + 9 : 4 + 17;
    }

    private static bool TryReadFrameHeader(ReadOnlySpan<byte> data, int startOffset, out Mp3FrameHeader frame)
    {
        frame = default;

        for (var offset = Math.Max(0, startOffset); offset + 4 <= data.Length; offset++)
        {
            var header = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (!TryParseHeader(header, out frame))
                continue;

            return true;
        }

        return false;
    }

    private static bool TryParseHeader(uint header, out Mp3FrameHeader frame)
    {
        frame = default;

        if ((header & 0xFFE00000) != 0xFFE00000)
            return false;

        var versionBits = (int)((header >> 19) & 0x3);
        var layerBits = (int)((header >> 17) & 0x3);
        var bitrateIndex = (int)((header >> 12) & 0xF);
        var sampleRateIndex = (int)((header >> 10) & 0x3);
        var padding = ((header >> 9) & 0x1) != 0;
        var channelMode = (int)((header >> 6) & 0x3);

        if (versionBits == 1 || layerBits == 0 || bitrateIndex is 0 or 15 || sampleRateIndex == 3)
            return false;

        var version = versionBits switch
        {
            3 => MpegVersion.Version1,
            2 => MpegVersion.Version2,
            0 => MpegVersion.Version25,
            _ => MpegVersion.Reserved
        };

        var layer = layerBits switch
        {
            3 => MpegLayer.Layer1,
            2 => MpegLayer.Layer2,
            1 => MpegLayer.Layer3,
            _ => MpegLayer.Reserved
        };

        var layerIndex = layer switch
        {
            MpegLayer.Layer1 => 0,
            MpegLayer.Layer2 => 1,
            MpegLayer.Layer3 => 2,
            _ => -1
        };

        if (version == MpegVersion.Reserved || layerIndex < 0)
            return false;

        var versionIndex = version == MpegVersion.Version1 ? 0 : 1;
        var bitrate = Bitrates[versionIndex, layerIndex, bitrateIndex] * 1000;
        if (bitrate <= 0)
            return false;

        var sampleRate = version switch
        {
            MpegVersion.Version1 => SampleRatesVersion1[sampleRateIndex],
            MpegVersion.Version2 => SampleRatesVersion2[sampleRateIndex],
            MpegVersion.Version25 => SampleRatesVersion25[sampleRateIndex],
            _ => 0
        };

        if (sampleRate <= 0)
            return false;

        var samplesPerFrame = SamplesPerFrame[versionIndex, layerIndex];
        var coefficient = samplesPerFrame / 8;
        var frameLength = layer == MpegLayer.Layer1
            ? (coefficient * bitrate / sampleRate + (padding ? 1 : 0)) * 4
            : coefficient * bitrate / sampleRate + (padding ? 1 : 0);

        if (frameLength <= 4 || frameLength > 16 * 1024)
            return false;

        frame = new Mp3FrameHeader(
            version,
            layer,
            sampleRate,
            channelMode == 3 ? 1 : 2,
            frameLength,
            samplesPerFrame);
        return true;
    }

    private readonly record struct Mp3FrameHeader(
        MpegVersion Version,
        MpegLayer Layer,
        int SampleRate,
        int Channels,
        int FrameLength,
        int SamplesPerFrame);

    private enum MpegVersion
    {
        Version25 = 0,
        Reserved = 1,
        Version2 = 2,
        Version1 = 3
    }

    private enum MpegLayer
    {
        Reserved = 0,
        Layer3 = 1,
        Layer2 = 2,
        Layer1 = 3
    }

    private static readonly int[,,] Bitrates =
    {
        {
            { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 },
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 },
            { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 }
        },
        {
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 },
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 },
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 }
        }
    };

    private static readonly int[,] SamplesPerFrame =
    {
        { 384, 1152, 1152 },
        { 384, 1152, 576 }
    };

    private static readonly int[] SampleRatesVersion1 = [44100, 48000, 32000];
    private static readonly int[] SampleRatesVersion2 = [22050, 24000, 16000];
    private static readonly int[] SampleRatesVersion25 = [11025, 12000, 8000];
}
