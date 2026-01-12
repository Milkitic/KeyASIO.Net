using Avalonia.Media;
using KeyAsio.Lang;
using KeyAsio.Shared;
using Material.Icons;

namespace KeyAsio.Services;

public enum PresetMode
{
    Standard,
    Lightweight,
    Extreme
}

public class PresetModel
{
    public PresetMode Mode { get; }
    public string Title { get; }
    public string Description { get; }
    public MaterialIconKind Icon { get; }
    public string Color { get; } // Hex color string or resource key

    public PresetModel(PresetMode mode, string title, string description, MaterialIconKind icon, string color)
    {
        Mode = mode;
        Title = title;
        Description = description;
        Icon = icon;
        Color = color;
    }
}

public class PresetManager
{
    private readonly AppSettings _appSettings;

    public PresetManager(AppSettings appSettings)
    {
        _appSettings = appSettings;

        if (!App.Current.TryGetResource("SukiWarningColor", null, out var val))
        {
            val = SolidColorBrush.Parse("#CD771D");
        }
    }

    public List<PresetModel> AvailablePresets { get; } = new()
    {
        new PresetModel(
            PresetMode.Standard,
            SR.Preset_Standard,
            "提供均衡的性能与资源占用，适合大多数常规使用场景",
            MaterialIconKind.ScaleBalance,
            "#2196F3"), // Blue
        new PresetModel(
            PresetMode.Lightweight,
            SR.Preset_Lightweight,
            "优化资源占用，适合低配设备或基础使用需求",
            MaterialIconKind.Feather,
            ), // Green

        new PresetModel(
            PresetMode.Extreme,
            SR.Preset_Extreme,
            "最大化性能输出，适合专业需求或高性能场景",
            MaterialIconKind.RocketLaunch,
            "#F44336") // Red
    };

    public void ApplyPreset(PresetMode mode)
    {
        switch (mode)
        {
            case PresetMode.Standard:
                ApplyStandard();
                break;
            case PresetMode.Lightweight:
                ApplyLightweight();
                break;
            case PresetMode.Extreme:
                ApplyExtreme();
                break;
        }
    }

    private void ApplyStandard()
    {
        //_appSettings.Input.UseRawInput = true;

        _appSettings.Audio.EnableLimiter = true;

        //_appSettings.Performance.EnableAvx512 = true; 

        _appSettings.Sync.Scanning.GeneralScanInterval = 50;
        _appSettings.Sync.Scanning.TimingScanInterval = 2;

        // todo: 平衡器算法、限频器算法、无视所有音量与声道变化等
    }

    private void ApplyLightweight()
    {
        _appSettings.Audio.EnableLimiter = true;

        _appSettings.Sync.Scanning.GeneralScanInterval = 50;
        _appSettings.Sync.Scanning.TimingScanInterval = 2;
    }

    private void ApplyExtreme()
    {
        _appSettings.Audio.EnableLimiter = false;

        _appSettings.Sync.Scanning.GeneralScanInterval = 50;
        _appSettings.Sync.Scanning.TimingScanInterval = 1;
    }
}