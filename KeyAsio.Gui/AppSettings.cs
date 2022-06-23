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

    private int _osuModeAudioOffset;

    [Description("If true, the software will enable memory scanning and play the right hitsounds of beatmaps.")]
    public bool OsuMode { get; set; } = true;

    [Description("The offset when `OsuMode` is true (allow adjusting in GUI).")]
    public int OsuModeAudioOffset
    {
        get => _osuModeAudioOffset;
        set
        {
            if (value == _osuModeAudioOffset) return;
            _osuModeAudioOffset = value;
            OnPropertyChanged();
        }
    }

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

    [Description("Default hitsound path (relative or absolute) for playing.")]
    public string HitsoundPath { get; set; } = "click.wav";

    [Description("The skin folder when `OsuMode` is true.")]
    public string SkinFolder { get; set; } = "";

    [Description("Show debug logs.")]
    public bool Debugging { get; set; } = false;

    [Description("Device's sample rate (allow adjusting in GUI).")]
    public int SampleRate { get; set; } = 48000;
    //public int Bits { get; set; } = 16;
    //public int Channels { get; set; } = 2;

    [Description("Device configuration (Recommend to configure in GUI).")]
    public DeviceDescription? Device { get; set; }

    [Description("Software volume control. Disable for extremely low latency when `OsuMode` is false.")]
    public bool VolumeEnabled { get; set; } = true;

    [Description("Configured device volume.")]
    public float Volume { get; set; } = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}