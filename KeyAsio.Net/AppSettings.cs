using System.ComponentModel;
using KeyAsio.Net.Configuration;
using KeyAsio.Net.Models;

namespace KeyAsio.Net;

public class AppSettings : ConfigurationBase
{
    [Description("Triggering keys. See https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-6.0 for more inforamtion.")]
    public HashSet<Hooking.Keys> Keys { get; set; } = new()
    {
        Hooking.Keys.Z,
        Hooking.Keys.X
    };

    [Description("Hitsound's relative or absolute path.")]
    public string HitsoundPath { get; set; } = "click.wav";
    public int SampleRate { get; set; } = 48000;
    public int Bits { get; set; } = 16;
    public int Channels { get; set; } = 2;
    [Description("Device configuration.")]
    public DeviceDescription? Device { get; set; }
}