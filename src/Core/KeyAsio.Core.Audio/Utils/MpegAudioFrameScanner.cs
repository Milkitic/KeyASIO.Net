using System.Buffers.Binary;

namespace KeyAsio.Core.Audio.Utils;

internal enum MpegVersion
{
    Version25 = 0,
    Reserved = 1,
    Version2 = 2,
    Version1 = 3
}

internal enum MpegLayer
{
    Reserved = 0,
    Layer3 = 1,
    Layer2 = 2,
    Layer1 = 3
}

internal readonly record struct MpegAudioFrameHeader(
    MpegVersion Version,
    MpegLayer Layer,
    int BitRate,
    int SampleRate,
    int Channels,
    int FrameLength,
    int SamplesPerFrame);

internal static class MpegAudioFrameScanner
{
    public const int DefaultMaxFrameHeaderScanBytes = 4096;

    private static readonly int[,,] s_bitrates =
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

    private static readonly int[] s_sampleRatesVersion1 = [44100, 48000, 32000];
    private static readonly int[] s_sampleRatesVersion2 = [22050, 24000, 16000];
    private static readonly int[] s_sampleRatesVersion25 = [11025, 12000, 8000];

    public static bool TryFindFirstFrame(
        ReadOnlySpan<byte> data,
        out MpegAudioFrameHeader frame,
        out int offset,
        bool requireLayer3 = false,
        bool validateNextFrameWhenAvailable = false,
        int maxScanBytes = DefaultMaxFrameHeaderScanBytes)
    {
        return TryFindFirstFrame(
            data,
            SkipId3v2(data),
            out frame,
            out offset,
            requireLayer3,
            validateNextFrameWhenAvailable,
            maxScanBytes);
    }

    public static bool TryFindFirstFrame(
        ReadOnlySpan<byte> data,
        int startOffset,
        out MpegAudioFrameHeader frame,
        out int offset,
        bool requireLayer3 = false,
        bool validateNextFrameWhenAvailable = false,
        int maxScanBytes = DefaultMaxFrameHeaderScanBytes)
    {
        frame = default;
        offset = -1;

        if (data.Length < 4)
            return false;

        var cursor = Math.Clamp(startOffset, 0, data.Length);
        if (cursor + 4 <= data.Length &&
            !TryParseHeader(BinaryPrimitives.ReadUInt32BigEndian(data.Slice(cursor, 4)), out _) &&
            data[cursor] == data[cursor + 1] &&
            data[cursor + 1] == data[cursor + 2] &&
            data[cursor + 2] == data[cursor + 3])
        {
            var paddingByte = data[cursor];
            while (cursor < data.Length && data[cursor] == paddingByte)
            {
                cursor++;
            }

            if (paddingByte == 0xFF && cursor > 0)
            {
                cursor--;
            }
        }

        var scanLength = Math.Max(0, maxScanBytes);
        var limit = Math.Min(data.Length, cursor + scanLength);
        for (var candidateOffset = cursor; candidateOffset + 4 <= limit; candidateOffset++)
        {
            if (data[candidateOffset] != 0xFF)
                continue;

            var header = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(candidateOffset, 4));
            if (!TryParseHeader(header, out var candidate))
                continue;

            if (requireLayer3 && candidate.Layer != MpegLayer.Layer3)
                continue;

            if (validateNextFrameWhenAvailable &&
                !HasConsistentNextFrame(data, candidateOffset, candidate))
            {
                continue;
            }

            frame = candidate;
            offset = candidateOffset;
            return true;
        }

        return false;
    }

    public static int SkipId3v2(ReadOnlySpan<byte> data)
    {
        return TryReadId3v2Size(data, out var size)
            ? Math.Min(data.Length, size)
            : 0;
    }

    public static bool TryReadId3v2Size(ReadOnlySpan<byte> data, out int totalSize)
    {
        totalSize = 0;

        if (data.Length < 10 || !data.Slice(0, 3).SequenceEqual("ID3"u8))
            return false;

        if (data[3] == 0xFF || data[4] == 0xFF ||
            (data[6] & 0x80) != 0 ||
            (data[7] & 0x80) != 0 ||
            (data[8] & 0x80) != 0 ||
            (data[9] & 0x80) != 0)
        {
            return false;
        }

        var payloadSize =
            (data[6] << 21) |
            (data[7] << 14) |
            (data[8] << 7) |
            data[9];
        var footerSize = (data[5] & 0x10) != 0 ? 10 : 0;
        totalSize = 10 + payloadSize + footerSize;
        return true;
    }

    public static bool TryParseHeader(uint header, out MpegAudioFrameHeader frame)
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
        var emphasis = (int)(header & 0x3);

        if (versionBits == 1 ||
            layerBits == 0 ||
            bitrateIndex is 0 or 15 ||
            sampleRateIndex == 3 ||
            emphasis == 2)
        {
            return false;
        }

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
        var bitrate = s_bitrates[versionIndex, layerIndex, bitrateIndex] * 1000;
        if (bitrate <= 0)
            return false;

        var sampleRate = version switch
        {
            MpegVersion.Version1 => s_sampleRatesVersion1[sampleRateIndex],
            MpegVersion.Version2 => s_sampleRatesVersion2[sampleRateIndex],
            MpegVersion.Version25 => s_sampleRatesVersion25[sampleRateIndex],
            _ => 0
        };

        if (sampleRate <= 0)
            return false;

        var samplesPerFrame = GetSamplesPerFrame(version, layer);
        var frameLength = GetFrameLength(version, layer, bitrate, sampleRate, padding);
        if (frameLength <= 4 || frameLength > 16 * 1024)
            return false;

        frame = new MpegAudioFrameHeader(
            version,
            layer,
            bitrate,
            sampleRate,
            channelMode == 3 ? 1 : 2,
            frameLength,
            samplesPerFrame);
        return true;
    }

    private static int GetSamplesPerFrame(MpegVersion version, MpegLayer layer)
    {
        return layer switch
        {
            MpegLayer.Layer1 => 384,
            MpegLayer.Layer2 => 1152,
            MpegLayer.Layer3 when version == MpegVersion.Version1 => 1152,
            MpegLayer.Layer3 => 576,
            _ => 0
        };
    }

    private static int GetFrameLength(
        MpegVersion version,
        MpegLayer layer,
        int bitrate,
        int sampleRate,
        bool padding)
    {
        var paddingSize = padding ? 1 : 0;
        if (layer == MpegLayer.Layer1)
        {
            return (12 * bitrate / sampleRate + paddingSize) * 4;
        }

        var coefficient = version == MpegVersion.Version1 || layer == MpegLayer.Layer2 ? 144 : 72;
        return coefficient * bitrate / sampleRate + paddingSize;
    }

    private static bool HasConsistentNextFrame(
        ReadOnlySpan<byte> data,
        int offset,
        MpegAudioFrameHeader frame)
    {
        var nextOffset = offset + frame.FrameLength;
        if (nextOffset + 4 > data.Length)
            return true;

        var nextHeader = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(nextOffset, 4));
        return TryParseHeader(nextHeader, out var nextFrame) &&
               nextFrame.Version == frame.Version &&
               nextFrame.Layer == frame.Layer;
    }
}
