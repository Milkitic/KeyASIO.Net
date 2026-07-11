namespace KeyAsio.Plugins.Contracts;

/// <summary>
/// Stable, plugin-facing preferences owned and persisted by the host.
/// </summary>
public interface IPluginSettings
{
    bool MusicMixingEnabled { get; set; }

    string? SkippedUpdateVersion { get; set; }

    event EventHandler? MusicMixingEnabledChanged;

    void Save();
}
