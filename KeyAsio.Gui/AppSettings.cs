using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyAsio.Gui.Configuration;
using Milki.Extensions.MixPlayer.Annotations;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Gui;

public sealed class AppSettings : ConfigurationBase, INotifyPropertyChanged
{
    private HashSet<HookKeys> _keys = new()
    {
        HookKeys.Z,
        HookKeys.X
    };

    public bool OsuMode { get; set; } = true;

    [Description("Triggering keys. See https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-6.0 for more inforamtion.")]
    public HashSet<HookKeys> Keys
    {
        get => _keys;
        set
        {
            if (Equals(value, _keys)) return;
            _keys = value;
            OnPropertyChanged();
        }
    }

    [Description("Hitsound's relative or absolute path.")]
    public string HitsoundPath { get; set; } = "click.wav";

    [Description("Hitsound's relative or absolute path.")]
    public string SkinFolder { get; set; } = "";

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

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}