using System.ComponentModel;
using KeyAsio.Configuration;
using KeyAsio.Plugins.Contracts;

namespace KeyAsio.Application.Plugins;

public sealed class PluginSettingsAdapter : IPluginSettings, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IAppSettingsPersistence _persistence;

    public PluginSettingsAdapter(AppSettings settings, IAppSettingsPersistence persistence)
    {
        _settings = settings;
        _persistence = persistence;
        _settings.Sync.PropertyChanged += OnSyncSettingsChanged;
    }

    public bool MusicMixingEnabled
    {
        get => _settings.Sync.EnableMixSync;
        set => _settings.Sync.EnableMixSync = value;
    }

    public string? SkippedUpdateVersion
    {
        get => _settings.Update.SkipVersion;
        set => _settings.Update.SkipVersion = value;
    }

    public event EventHandler? MusicMixingEnabledChanged;

    public void Save() => _persistence.Save();

    public void Dispose() => _settings.Sync.PropertyChanged -= OnSyncSettingsChanged;

    private void OnSyncSettingsChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(AppSettingsSync.EnableMixSync))
        {
            MusicMixingEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
