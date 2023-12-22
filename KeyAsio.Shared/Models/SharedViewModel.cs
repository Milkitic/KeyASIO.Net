using System.Collections.ObjectModel;
using System.IO;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Waves;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.Devices;

namespace KeyAsio.Shared.Models;

public class SharedViewModel : ViewModelBase
{
    private AudioEngine? _audioEngine;
    private DeviceDescription? _deviceDescription;
    private int _framesPerBuffer;
    private int _playbackLatency;
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
        set => SetField(ref _selectedSkin, value);
    }

    public AudioEngine? AudioEngine
    {
        get => _audioEngine;
        set => SetField(ref _audioEngine, value);
    }

    public DeviceDescription? DeviceDescription
    {
        get => _deviceDescription;
        set => SetField(ref _deviceDescription, value);
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public int FramesPerBuffer
    {
        get => _framesPerBuffer;
        set => SetField(ref _framesPerBuffer, value);
    }

    public int PlaybackLatency
    {
        get => _playbackLatency;
        set => SetField(ref _playbackLatency, value);
    }

    public RealtimeModeManager RealtimeModeManager { get; } = RealtimeModeManager.Instance;
    public string DefaultFolder { get; } = Path.Combine(Environment.CurrentDirectory, "resources", "default");
    public bool LatencyTestMode { get; set; }
}