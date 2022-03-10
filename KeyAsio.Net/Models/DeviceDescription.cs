using System;
using System.ComponentModel;

namespace KeyAsio.Net.Models;

public record DeviceDescription
{
    [Description("Available for WASAPI, DirectSound (Guid)")]
    public WavePlayerType WavePlayerType { get; init; }

    [Description("Available for ASIO,WASAPI, DirectSound (Guid)")]
    public string? DeviceId { get; init; }

    //[Description("Available for WASAPI, DirectSound")]
    //public string FriendlyName { get; set; }

    [Description("Available for WASAPI, DirectSound")]
    public int Latency { get; init; }
    [Description("Available for WASAPI")]
    public bool IsExclusive { get; init; }

    public static DeviceDescription WasapiDefault { get; } = new()
    {
        WavePlayerType = WavePlayerType.WASAPI
    };

    public static DeviceDescription DirectSoundDefault { get; } = new()
    {
        WavePlayerType = WavePlayerType.DirectSound,
        DeviceId = Guid.Empty.ToString()
    };
}