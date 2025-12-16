namespace KeyAsio.Shared.OsuMemory;

public readonly record struct BeatmapIdentifier
{
    public BeatmapIdentifier(string? filenameFull)
    {
        FilenameFull = filenameFull;
        if (filenameFull != null)
        {
            Folder = Path.GetDirectoryName(filenameFull);
            Filename = Path.GetFileName(filenameFull);
        }
    }

    public BeatmapIdentifier(string? folder, string? filename)
    {
        Folder = folder;
        Filename = filename;
        if (folder != null && filename != null)
        {
            FilenameFull = Path.Combine(folder, filename);
        }
    }

    public string? FilenameFull { get; }
    public string? Folder { get; }
    public string? Filename { get; }

    public static BeatmapIdentifier Default = new();
}