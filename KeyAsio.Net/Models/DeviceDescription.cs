using System.ComponentModel;
using YamlDotNet.Serialization;

namespace KeyAsio.Net.Models;

public class DeviceDescription
{
    [Description("Support types: ASIO, WASAPI, DirectSound")]
    public WavePlayerType WavePlayerType { get; init; }

    [Description("Available for ASIO, WASAPI, DirectSound (Guid)")]
    public string? DeviceId { get; init; }

    [Description("Available for WASAPI, DirectSound")]
    [YamlIgnore]
    public string? FriendlyName { get; init; }

    [Description("Available for WASAPI (excluded >= 3ms, non-excluded >= 0ms), DirectSound (around >= 20ms)")]
    public int Latency { get; init; }

    [Description("Available for WASAPI")]
    public bool IsExclusive { get; init; }

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

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((DeviceDescription)obj);
    }

    protected bool Equals(DeviceDescription other)
    {
        return WavePlayerType == other.WavePlayerType && DeviceId == other.DeviceId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)WavePlayerType, DeviceId);
    }

    public static bool operator ==(DeviceDescription? left, DeviceDescription? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DeviceDescription? left, DeviceDescription? right)
    {
        return !Equals(left, right);
    }
}