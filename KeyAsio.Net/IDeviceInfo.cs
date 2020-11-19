namespace KeyAsio.Net
{
    public interface IDeviceInfo
    {
        OutputMethod OutputMethod { get; }
        string FriendlyName { get; }
        //public override string ToString()
        //{
        //    return $"({OutputMethod}) {FriendlyName}";
        //}
    }
}