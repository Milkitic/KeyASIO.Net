using System.Collections.ObjectModel;
using KeyAsio.Audio;

namespace KeyAsio.Shared.Models;

public class SharedViewModel : ViewModelBase
{
    private DeviceDescription? _deviceDescription;
    private int _framesPerBuffer;
    private int _playbackLatency;
    private SkinDescription? _selectedSkin;

    public SharedViewModel(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }
    public RangeObservableCollection<SkinDescription> Skins { get; } = [SkinDescription.Default];

    public SkinDescription? SelectedSkin
    {
        get => _selectedSkin;
        set => SetField(ref _selectedSkin, value);
    }

    public DeviceDescription? DeviceDescription
    {
        get => _deviceDescription;
        set => SetField(ref _deviceDescription, value);
    }

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
    public bool AutoMode { get; set; }

    public string DefaultFolder { get; } = Path.Combine(Environment.CurrentDirectory, "resources", "default");
    public AppSettings AppSettings { get; }
}