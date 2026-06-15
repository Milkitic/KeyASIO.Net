using System.ComponentModel;
using System.Runtime.Serialization;

namespace KeyAsio.Core.Audio;

public record DeviceDescription
{
    [Description("Support types: ASIO, WASAPI, DirectSound, SDL, SDL3")]
    public WavePlayerType WavePlayerType { get; init; }

    [Description("Available for ASIO, WASAPI, DirectSound (Guid), SDL/SDL3 (device name)")]
    public string? DeviceId { get; init; }

    [Description("Available for WASAPI, DirectSound")]
    [IgnoreDataMember]
    public string? FriendlyName { get; init; }

    [Description("Available for WASAPI (excluded >= 3ms, non-excluded >= 0ms), DirectSound (around >= 20ms), SDL/SDL3 (converted to buffer frames by upper layer)")]
    public int Latency { get; init; }

    [Description("Available for ASIO, zero for preffered buffer size from driver.")]
    public ushort ForceASIOBufferSize { get; init; }

    [Description("Available for WASAPI")]
    public bool IsExclusive { get; init; }

    [IgnoreDataMember]
    public double AsioLatencyMs { get; init; }

    [IgnoreDataMember]
    public int AsioActualSamples { get; init; }

    public static DeviceDescription WasapiDefault { get; } = new()
    {
        WavePlayerType = WavePlayerType.WASAPI,
        FriendlyName = "WASAPI Auto"
    };

    public static DeviceDescription DirectSoundDefault { get; } = new()
    {
        WavePlayerType = WavePlayerType.DirectSound,
        DeviceId = Guid.Empty.ToString(),
        FriendlyName = "DirectSound Default"
    };
}
