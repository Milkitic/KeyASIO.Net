using ATL;

namespace KeyAsio.Audio.Utils;

public static class FileFormatHelper
{
    public static FileFormat DetermineFileFormatFromStream(Stream sourceStream)
    {
        var track = new Track(sourceStream);
        if (track.AudioFormat.ShortName == "WAV")
        {
            return FileFormat.Wav;
        }

        if (track.AudioFormat.ShortName == "OGG")
        {
            return FileFormat.Ogg;
        }

        if (track.AudioFormat.ShortName == "AIFF")
        {
            return FileFormat.Aiff;
        }

        if (track.AudioFormat.ShortName == "FLAC")
        {
            return FileFormat.Flac;
        }

        if (track.AudioFormat.ShortName == "WMA")
        {
            return FileFormat.Wma;
        }

        if (track.AudioFormat.Name == "MPEG Audio (Layer III)")
        {
            return track.MetadataFormats.Count > 0 ? FileFormat.Mp3Id3 : FileFormat.Mp3;
        }

        return FileFormat.Others;
    }
}