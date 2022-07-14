using System;
using System.Collections.ObjectModel;
using System.IO;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Realtime;
using KeyAsio.Gui.Waves;
using Milki.Extensions.MixPlayer.Devices;

namespace KeyAsio.Gui.Models;

public class SharedViewModel : ViewModelBase
{
    private AudioEngine? _audioEngine;
    private DeviceDescription? _deviceDescription;
    private int _framesPerBuffer;
    private int _playbackLatency;
    private bool _debugging;
    private SkinDescription? _selectedSkin;

    private SharedViewModel()
    {
    }

    public static SharedViewModel Instance { get; } = new();

    public ObservableCollection<SkinDescription> Skins { get; } = new()
    {
        SkinDescription.Default
    };

    public SkinDescription? SelectedSkin
    {
        get => _selectedSkin;
        set => this.RaiseAndSetIfChanged(ref _selectedSkin, value);
    }

    public AudioEngine? AudioEngine
    {
        get => _audioEngine;
        set => this.RaiseAndSetIfChanged(ref _audioEngine, value);
    }

    public DeviceDescription? DeviceDescription
    {
        get => _deviceDescription;
        set => this.RaiseAndSetIfChanged(ref _deviceDescription, value);
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public int FramesPerBuffer
    {
        get => _framesPerBuffer;
        set => this.RaiseAndSetIfChanged(ref _framesPerBuffer, value);
    }

    public int PlaybackLatency
    {
        get => _playbackLatency;
        set => this.RaiseAndSetIfChanged(ref _playbackLatency, value);
    }

    public bool Debugging
    {
        get => _debugging;
        set => this.RaiseAndSetIfChanged(ref _debugging, value);
    }

    public RealtimeModeManager RealtimeModeManager { get; } = RealtimeModeManager.Instance;
    public string DefaultFolder { get; } = Path.Combine(Environment.CurrentDirectory, "Resources", "default");
    public bool LatencyTestMode { get; set; }
}