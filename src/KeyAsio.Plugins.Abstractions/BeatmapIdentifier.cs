namespace KeyAsio.Plugins.Abstractions;

public class BeatmapIdentifier
{
    public int SetId { get; set; }
    public int MapId { get; set; }
    public string? Md5 { get; set; }
    public string? Folder { get; set; }
    public string? Filename { get; set; }
}