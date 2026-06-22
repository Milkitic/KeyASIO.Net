using ATL.AudioData;

namespace KeyAsio.Core.Audio.Utils;

public static class FileFormatHelper
{
    private const int HeaderProbeLength = 64;
    private static readonly byte[] s_asfHeader =
    [
        0x30, 0x26, 0xB2, 0x75,
        0x8E, 0x66, 0xCF, 0x11,
        0xA6, 0xD9, 0x00, 0xAA,
        0x00, 0x62, 0xCE, 0x6C
    ];

    public static FileFormat DetermineFileFormatFromStream(Stream sourceStream)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        if (!sourceStream.CanSeek)
            throw new ArgumentException("Stream must be seekable.", nameof(sourceStream));

        var initialPosition = sourceStream.Position;
        try
        {
            sourceStream.Seek(0, SeekOrigin.Begin);

            Span<byte> headerBuffer = stackalloc byte[HeaderProbeLength];
            var headerBytesRead = sourceStream.Read(headerBuffer);
            var header = headerBuffer.Slice(0, headerBytesRead);

            var knownFormat = DetermineKnownFormatFromHeader(header);
            if (knownFormat != FileFormat.Others)
                return knownFormat;

            if (TryDetectMpegLayer3(sourceStream, header))
            {
                return MpegAudioFrameScanner.TryReadId3v2Size(header, out _)
                    ? FileFormat.Mp3Id3
                    : FileFormat.Mp3;
            }

            return DetermineWithAtlFactory(sourceStream, header);
        }
        finally
        {
            sourceStream.Seek(initialPosition, SeekOrigin.Begin);
        }
    }

    private static FileFormat DetermineKnownFormatFromHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 12 &&
            (header.Slice(0, 4).SequenceEqual("RIFF"u8) ||
             header.Slice(0, 4).SequenceEqual("RF64"u8)) &&
            header.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return FileFormat.Wav;
        }

        if (header.Length >= 4)
        {
            if (header.Slice(0, 4).SequenceEqual("OggS"u8))
                return FileFormat.Ogg;

            if (header.Slice(0, 4).SequenceEqual("fLaC"u8))
                return FileFormat.Flac;
        }

        if (header.Length >= 12 &&
            header.Slice(0, 4).SequenceEqual("FORM"u8) &&
            (header.Slice(8, 4).SequenceEqual("AIFF"u8) ||
             header.Slice(8, 4).SequenceEqual("AIFC"u8)))
        {
            return FileFormat.Aiff;
        }

        return header.Length >= s_asfHeader.Length &&
               header.Slice(0, s_asfHeader.Length).SequenceEqual(s_asfHeader)
            ? FileFormat.Wma
            : FileFormat.Others;
    }

    private static FileFormat DetermineWithAtlFactory(Stream sourceStream, ReadOnlySpan<byte> header)
    {
        try
        {
            var reader = AudioDataIOFactory.GetInstance().GetFromStream(sourceStream);
            var format = reader.AudioFormat;
            var containerId = format.ContainerId;
            var dataFormatId = format.DataFormat.ID;

            if (containerId == AudioDataIOFactory.CID_MPEG ||
                dataFormatId == AudioDataIOFactory.CID_MPEG)
            {
                return TryDetectMpegLayer3(sourceStream, header)
                    ? MpegAudioFrameScanner.TryReadId3v2Size(header, out _) ? FileFormat.Mp3Id3 : FileFormat.Mp3
                    : FileFormat.Others;
            }

            return containerId switch
            {
                AudioDataIOFactory.CID_OGG => FileFormat.Ogg,
                AudioDataIOFactory.CID_FLAC => FileFormat.Flac,
                AudioDataIOFactory.CID_WMA => FileFormat.Wma,
                AudioDataIOFactory.CID_AIFF => FileFormat.Aiff,
                AudioDataIOFactory.CID_WAV when DetermineKnownFormatFromHeader(header) == FileFormat.Wav =>
                    FileFormat.Wav,
                _ => FileFormat.Others
            };
        }
        catch
        {
            return FileFormat.Others;
        }
    }

    private static bool TryDetectMpegLayer3(Stream sourceStream, ReadOnlySpan<byte> header)
    {
        var scanStart = MpegAudioFrameScanner.TryReadId3v2Size(header, out var id3v2Size)
            ? id3v2Size
            : 0;

        if (scanStart >= sourceStream.Length)
            return false;

        Span<byte> scanBuffer = stackalloc byte[MpegAudioFrameScanner.DefaultMaxFrameHeaderScanBytes];
        sourceStream.Seek(scanStart, SeekOrigin.Begin);
        var bytesRead = sourceStream.Read(scanBuffer);
        return MpegAudioFrameScanner.TryFindFirstFrame(
            scanBuffer.Slice(0, bytesRead),
            0,
            out _,
            out _,
            requireLayer3: true,
            validateNextFrameWhenAvailable: true);
    }
}
