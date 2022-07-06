using System;
using System.IO;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Realtime;
using KeyAsio.Gui.Waves;
using Milki.Extensions.MixPlayer.Devices;

namespace KeyAsio.Gui.Models;

public class SharedViewModel : ViewModelBase
{
    private AudioEngine? _audioPlaybackEngine;
    private DeviceDescription? _deviceDescription;
    private int _framesPerBuffer;
    private int _playbackLatency;
    private bool _debugging;

    private SharedViewModel()
    {
    }

    public static SharedViewModel Instance { get; } = new();

    public AudioEngine? AudioPlaybackEngine
    {
        get => _audioPlaybackEngine;
        set => this.RaiseAndSetIfChanged(ref _audioPlaybackEngine, value);
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