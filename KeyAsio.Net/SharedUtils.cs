namespace KeyAsio.Net;

public static class SharedUtils
{
    public static byte[] EmptyWaveFile =
    {
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x44, 0xAC, 0x00, 0x00, 0x10, 0xB1, 0x02, 0x00,
        0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x00
    };

    public static string CountSize(long size)
    {
        string strSize = "";
        long factSize = size;
        if (factSize < 1024)
            strSize = $"{factSize:F2} B";
        else if (factSize is >= 1024 and < 1048576)
            strSize = (factSize / 1024f).ToString("F2") + " KB";
        else if (factSize is >= 1048576 and < 1073741824)
            strSize = (factSize / 1024f / 1024f).ToString("F2") + " MB";
        else if (factSize >= 1073741824)
            strSize = (factSize / 1024f / 1024f / 1024f).ToString("F2") + " GB";
        return strSize;
    }
}