namespace KeyAsio.Plugins.DefaultMusic;

public class Configuration
{
    private Configuration()
    {
    }

    public static Configuration Instance { get; } = new();
    public string SoundTouchDir { get; set; } = Path.Combine(Environment.CurrentDirectory, "runtimes");
}