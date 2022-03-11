using KeyAsio.Net.Configuration;
using KeyAsio.Net.Models;

namespace KeyAsio.Net;

public class AppSettings : ConfigurationBase
{
    public HashSet<Hooking.Keys> Keys { get; set; } = new()
    {
        Hooking.Keys.Z,
        Hooking.Keys.X
    };

    public string HitsoundPath { get; set; } = "click.wav";
    public int SampleRate { get; set; } = 48000;
    public int Bits { get; set; } = 16;
    public int Channels { get; set; } = 2;
    public DeviceDescription? Device { get; set; }
}