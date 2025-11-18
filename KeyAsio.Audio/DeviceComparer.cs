namespace KeyAsio.Audio;

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
}