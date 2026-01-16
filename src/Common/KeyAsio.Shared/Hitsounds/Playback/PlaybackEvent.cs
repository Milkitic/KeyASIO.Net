namespace KeyAsio.Shared.Hitsounds.Playback;

public abstract class PlaybackEvent
{
    /// <summary>
    /// 0 - 1.0f
    /// </summary>
    public float Balance { get; internal set; }
    public string? Filename { get; internal set; }
    public double Offset { get; internal set; }
    public bool UseUserSkin { get; internal set; }

    /// <summary>
    /// 0 - 1.0f
    /// </summary>
    public float Volume { get; internal set; }

    public static SampleEvent Create(
        Guid guid,
        double offset,
        float volume,
        float balance,
        string filename,
        bool useUserSkin,
        SampleLayer layer)
    {
        var soundElement = new SampleEvent
        {
            Guid = guid,
            Offset = offset,
            Volume = volume,
            Balance = balance,
            Filename = filename,
            UseUserSkin = useUserSkin,
            Layer = layer
        };
        return soundElement;
    }

    public static ControlEvent CreateLoopSignal(
        double offset,
        float volume,
        float balance,
        string filename,
        bool useUserSkin,
        LoopChannel loopChannel)
    {
        return new ControlEvent
        {
            Offset = offset,
            Volume = volume,
            Balance = balance,
            Filename = filename,
            UseUserSkin = useUserSkin,

            ControlEventType = ControlEventType.LoopStart,
            LoopChannel = loopChannel
        };
    }

    public static ControlEvent CreateLoopStopSignal(int offset, LoopChannel loopChannel)
    {
        return new ControlEvent
        {
            Offset = offset,

            ControlEventType = ControlEventType.LoopStop,
            LoopChannel = loopChannel
        };
    }

    public static ControlEvent CreateLoopVolumeSignal(int offset, float volume)
    {
        return new ControlEvent
        {
            Offset = offset,
            Volume = volume,

            ControlEventType = ControlEventType.Volume
        };
    }

    public static ControlEvent CreateLoopBalanceSignal(int offset, float balance)
    {
        return new ControlEvent
        {
            Offset = offset,
            Balance = balance,

            ControlEventType = ControlEventType.Balance
        };
    }
}