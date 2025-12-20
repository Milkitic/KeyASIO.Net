namespace KeyAsio.Plugins.DefaultMusic;

public class VariableSpeedOptions
{
    public bool KeepTune { get; set; }
    public bool UseAntiAliasing { get; set; }
    public bool UseQuickSeek { get; set; } = true;

    public VariableSpeedOptions(bool keepTune, bool useAntiAliasing)
    {
        KeepTune = keepTune;
        UseAntiAliasing = useAntiAliasing;
    }
}