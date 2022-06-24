using System;
using System.IO;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.NAudioExtensions;

namespace KeyAsio.Gui;

public class SharedViewModel : ViewModelBase
{
    private AudioPlaybackEngine? _audioPlaybackEngine;
    private DeviceDescription? _deviceDescription;
    private AppSettings? _appSettings;
    private int _framesPerBuffer;
    private int _playbackLatency;
    private bool _debugging;

    private SharedViewModel()
    {
    }

    public static SharedViewModel Instance { get; } = new();

    public AudioPlaybackEngine? AudioPlaybackEngine
    {
        get => _audioPlaybackEngine;
        set => this.RaiseAndSetIfChanged(ref _audioPlaybackEngine, value);
    }

    public DeviceDescription? DeviceDescription
    {
        get => _deviceDescription;
        set => this.RaiseAndSetIfChanged(ref _deviceDescription, value);
    }

    public AppSettings? AppSettings
    {
        get => _appSettings;
        set => this.RaiseAndSetIfChanged(ref _appSettings, value);
    }

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