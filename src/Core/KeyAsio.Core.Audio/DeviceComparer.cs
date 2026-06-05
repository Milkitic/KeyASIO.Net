namespace KeyAsio.Core.Audio;

public class DeviceComparer : IEqualityComparer<DeviceDescription>
{
    private DeviceComparer()
    {
    }

    public static DeviceComparer Instance { get; } = new();

    public bool Equals(DeviceDescription? x, DeviceDescription? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return x.WavePlayerType == y.WavePlayerType && x.DeviceId == y.DeviceId;
    }

    public int GetHashCode(DeviceDescription obj)
    {
        return HashCode.Combine((int)obj.WavePlayerType, obj.DeviceId);
    }

    public static bool AreSettingsEqual(DeviceDescription? d1, DeviceDescription? d2)
    {
        if (ReferenceEquals(d1, d2)) return true;
        if (d1 is null || d2 is null) return false;
        if (d1.WavePlayerType != d2.WavePlayerType || d1.DeviceId != d2.DeviceId) return false;

        if (d1.WavePlayerType == WavePlayerType.ASIO)
        {
            return d1.ForceASIOBufferSize == d2.ForceASIOBufferSize;
        }

        if (d1.WavePlayerType == WavePlayerType.WASAPI)
        {
            return d1.Latency == d2.Latency && d1.IsExclusive == d2.IsExclusive;
        }

        return d1.Latency == d2.Latency;
    }
}