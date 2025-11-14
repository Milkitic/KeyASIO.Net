namespace KeyAsio.Audio;

public class Configuration
{
    private Configuration()
    {
    }

    public static Configuration Instance { get; } = new();

    public uint GeneralOffset { get; set; } = 0;
    public float PlaybackRate { get; set; } = 1;
    public bool KeepTune { get; set; } = false;
    public string DefaultDir { get; set; } = Path.Combine(Environment.CurrentDirectory, "default");
    public string CacheDir { get; set; } = Path.Combine(Environment.CurrentDirectory, "caching");
    public string SoundTouchDir { get; set; } = Path.Combine(Environment.CurrentDirectory, "runtimes");
}