using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared;

namespace KeyAsio.ViewModels;

public partial class PluginManagerViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _appSettings;
    private readonly List<IMusicManagerPlugin> _musicManagerPlugins = new();
    private bool _disposed;

    public PluginManagerViewModel(IPluginManager pluginManager, AppSettings appSettings)
    {
        _appSettings = appSettings;
        _musicManagerPlugins.AddRange(pluginManager.GetAllPlugins().OfType<IMusicManagerPlugin>());
        foreach (var plugin in _musicManagerPlugins)
        {
            plugin.OptionStateChanged += OnMixOptionStateChanged;
        }

        _appSettings.Sync.PropertyChanged += OnSyncSettingsChanged;
        RefreshMixMode();
    }

    [ObservableProperty]
    public partial string? MixModeDisplayName { get; set; }

    [ObservableProperty]
    public partial bool IsMixSwitchEnabled { get; set; }

    [ObservableProperty]
    public partial IMusicManagerPlugin? ActivePlugin { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMixModeTagPro))]
    [NotifyPropertyChangedFor(nameof(HasMixModeTag))]
    public partial string? MixModeTag { get; set; }

    public bool IsMixModeTagPro
    {
        get
        {
            var isMixModeTagPro = string.Equals(MixModeTag, "PRO", StringComparison.OrdinalIgnoreCase);
            return isMixModeTagPro;
        }
    }

    public bool HasMixModeTag => !string.IsNullOrWhiteSpace(MixModeTag);

    private void OnMixOptionStateChanged(object? sender, EventArgs e)
    {
        RefreshMixMode();
    }

    private void OnSyncSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsSync.EnableSync))
        {
            RefreshMixMode();
        }
    }

    private void RefreshMixMode()
    {
        IMusicManagerPlugin? selected = _musicManagerPlugins
            .Where(x => x.CanEnableOption)
            .OrderByDescending(x => x.OptionPriority)
            .FirstOrDefault();

        selected ??= _musicManagerPlugins
            .OrderByDescending(x => x.OptionPriority)
            .FirstOrDefault();
        ActivePlugin = selected;
        MixModeDisplayName = selected?.OptionName ?? "Corrupted";
        MixModeTag = selected?.OptionTag;
        IsMixSwitchEnabled = _appSettings.Sync.EnableSync && (selected?.CanEnableOption ?? false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var plugin in _musicManagerPlugins)
        {
            plugin.OptionStateChanged -= OnMixOptionStateChanged;
        }

        _appSettings.Sync.PropertyChanged -= OnSyncSettingsChanged;
        GC.SuppressFinalize(this);
    }
}
