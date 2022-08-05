using System;
using System.Collections.Generic;
using System.ComponentModel;
using KeyAsio.Gui.Models;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Gui;

public sealed class AppSettings : ViewModelBase
{
    private List<HookKeys> _keys = new()
    {
        HookKeys.Z,
        HookKeys.X
    };

    private RealtimeOptions? _realtimeOptions;
    private int _volume = 100;
    private bool _sendLogsToDeveloper = true;
    private string? _osuFolder = "";
    private bool _debugging = false;

    [Description("Triggering keys. See https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-6.0 for more inforamtion.")]
    public List<HookKeys> Keys
    {
        get => _keys;
        set => this.RaiseAndSetIfChanged(ref _keys, value);
    }

    [Description("Default hitsound path (relative or absolute) for playing.")]
    public string HitsoundPath { get; set; } = "click.wav";

    [Description("Osu's folder. For the most of time this value is auto detected.")]
    public string? OsuFolder
    {
        get => _osuFolder;
        set => this.RaiseAndSetIfChanged(ref _osuFolder, value);
    }

    [Description("The skin when `RealtimeMode` is true.")]
    public string SelectedSkin { get; set; } = "";

    [Description("If true, the software will create a console window to show logs.")]
    public bool Debugging
    {
        get => _debugging;
        set => this.RaiseAndSetIfChanged(ref _debugging, value);
    }

    [Description("Device's sample rate (allow adjusting in GUI).")]
    public int SampleRate { get; set; } = 48000;
    //public int Bits { get; set; } = 16;
    //public int Channels { get; set; } = 2;

    [Description("Device configuration (Recommend to configure in GUI).")]
    public DeviceDescription? Device { get; set; }

    [Description("Configured device volume, range: 0~150")]
    public float Volume
    {
        get => _volume;
        set
        {
            if (value > 150) value = 150;
            else if (value < 0) value = 0;
            else if (value < 1) value *= 100; // Convert from old version
            _volume = (int)Math.Round(value);
        }
    }

    [Description("Set whether the software can report logs/bugs to developer.")]
    public bool SendLogsToDeveloper
    {
        get => _sendLogsToDeveloper;
        set => this.RaiseAndSetIfChanged(ref _sendLogsToDeveloper, value);
    }

    public bool SendLogsToDeveloperConfirmed { get; set; }

    public RealtimeOptions RealtimeOptions
    {
        get => _realtimeOptions ??= new();
        set => _realtimeOptions = value;
    }

    public string PlayerBase64 { get; set; } = "";

    public void Save()
    {
        ConfigurationFactory.Save(this);
    }
}