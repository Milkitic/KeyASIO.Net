using KeyAsio.Core.Audio;
using KeyAsio.Common;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Models;
using KeyAsio.Sync.Abstractions;

namespace KeyAsio.Application.Models;

public class ApplicationState : ViewModelBase, IPlaybackRuntimeState
{
    private DeviceDescription? _deviceDescription;
    private int _framesPerBuffer;
    private int _playbackLatency;
    private SkinDescription? _selectedSkin;

    public ApplicationState(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }
    public ObservableRangeCollection<SkinDescription> Skins { get; } = [SkinDescription.Internal];

    public SkinDescription? SelectedSkin
    {
        get => _selectedSkin;
        set
        {
            if (!SetField(ref _selectedSkin, value)) return;
            SelectedSkinChanged?.Invoke();
        }
    }

    public string SelectedSkinFolder => SelectedSkin?.Folder ?? string.Empty;

    public event Action? SelectedSkinChanged;

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
