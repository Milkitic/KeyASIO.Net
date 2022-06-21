using System.Collections.Generic;
using System.ComponentModel;
using KeyAsio.Gui.Configuration;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Gui;

public class AppSettings : ConfigurationBase
{
    [Description("Triggering keys. See https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-6.0 for more inforamtion.")]
    public HashSet<HookKeys> Keys { get; set; } = new()
    {
        HookKeys.Z,
        HookKeys.X
    };

    [Description("Hitsound's relative or absolute path.")]
    public string HitsoundPath { get; set; } = "click.wav";

    [Description("Show output while pressing buttons.")]
    public bool Debugging { get; set; } = false;

    public int SampleRate { get; set; } = 48000;
    public int Bits { get; set; } = 16;
    public int Channels { get; set; } = 2;
    [Description("Device configuration.")]
    public DeviceDescription? Device { get; set; }

    [Description("Enable volume control.")]
    public bool VolumeEnabled { get; set; } = true;

    [Description("Last volume.")] 
    public float Volume { get; set; } = 1;
}