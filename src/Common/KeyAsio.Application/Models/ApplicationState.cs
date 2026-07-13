using KeyAsio.Common;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Models;
using KeyAsio.Core.Audio;
using KeyAsio.Sync.Abstractions;

namespace KeyAsio.Application.Models;

public class ApplicationState : ViewModelBase, IPlaybackRuntimeState
{
    public ApplicationState(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }

    public ObservableRangeCollection<SkinDescription> Skins { get; } = [SkinDescription.Internal];

    public SkinDescription? SelectedSkin
    {
        get;
        set
        {
            if (!SetField(ref field, value)) return;
            SelectedSkinChanged?.Invoke();
        }
    }

    public string SelectedSkinFolder => SelectedSkin?.Folder ?? string.Empty;

    public event Action? SelectedSkinChanged;

    public DeviceDescription? DeviceDescription
    {
        get;
        set => SetField(ref field, value);
    }

    public int FramesPerBuffer
    {
        get;
        set => SetField(ref field, value);
    }

    public int PlaybackLatency
    {
        get;
        set => SetField(ref field, value);
    }

    public bool AutoMode { get; set; }

    public string DefaultFolder { get; } = Path.Combine(Environment.CurrentDirectory, "resources", "default");
    public AppSettings AppSettings { get; }
}
