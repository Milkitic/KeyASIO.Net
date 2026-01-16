namespace KeyAsio.Shared.Hitsounds.Playback;

public abstract class PlaybackEvent
{
    /// <summary>
    /// 0 - 1.0f
    /// </summary>
    public float Balance { get; private set; }
    public string? Filename { get; private set; }
    public double Offset { get; private set; }
    public ResourceOwner ResourceOwner { get; private set; }

    /// <summary>
    /// 0 - 1.0f
    /// </summary>
    public float Volume { get; private set; }

    public static SampleEvent Create(
        Guid guid,
        double offset,
        float volume,
        float balance,
        string filename,
        ResourceOwner resourceOwner,
        SampleLayer layer)
    {
        var soundElement = new SampleEvent
        {
            Guid = guid,
            Offset = offset,
            Volume = volume,
            Balance = balance,
            Filename = filename,
            ResourceOwner = resourceOwner,
            Layer = layer
        };
        return soundElement;
    }

    public static ControlEvent CreateLoopSignal(
        double offset,
        float volume,
        float balance,
        string filename,
        ResourceOwner resourceOwner,
        LoopChannel loopChannel)
    {
        return new ControlEvent
        {
            Offset = offset,
            Volume = volume,
            Balance = balance,
            Filename = filename,
            ResourceOwner = resourceOwner,

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