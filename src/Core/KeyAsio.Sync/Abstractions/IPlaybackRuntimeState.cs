namespace KeyAsio.Sync.Abstractions;

public interface IPlaybackRuntimeState
{
    bool AutoMode { get; }

    string SelectedSkinFolder { get; }

    event Action? SelectedSkinChanged;
}
