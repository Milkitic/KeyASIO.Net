using KeyAsio.Lang;
using KeyAsio.Shared;
using KeyAsio.ViewModels;
using Material.Icons;

namespace KeyAsio.Services;

public enum PresetMode
{
    Standard,
    Fast,
    Extreme
}

public class PresetModel
{
    public PresetMode Mode { get; }
    public string Title { get; }
    public string Description { get; }
    public MaterialIconKind Icon { get; }
    public string ColorOrKey { get; }

    public PresetModel(PresetMode mode, string title, string description, MaterialIconKind icon, string colorOrKey)
    {
        Mode = mode;
        Title = title;
        Description = description;
        Icon = icon;
        ColorOrKey = colorOrKey;
    }
}

public class PresetManager
{
    private readonly AppSettings _appSettings;

    public PresetManager(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public List<PresetModel> AvailablePresets { get; private set; } = [];

    public void Initialize()
    {
        AvailablePresets =
        [
            new PresetModel(
                PresetMode.Standard,
                SRKeys.Preset_Standard,
                "提供均衡的性能与资源占用，适合大多数常规使用场景",
                MaterialIconKind.ScaleBalance,
                "SukiInformationColor"
            ),
            new PresetModel(
                PresetMode.Fast,
                SRKeys.Preset_Lightweight,
                "优化资源占用，适合低配设备或基础使用需求",
                MaterialIconKind.Feather,
                "#D01373"
            ),
            new PresetModel(
                PresetMode.Extreme,
                SRKeys.Preset_Extreme,
                "最大化性能输出，适合专业需求或高性能场景",
                MaterialIconKind.RocketLaunch,
                "SukiDangerColor")
        ];
    }

    public async Task ApplyPreset(PresetMode mode, AudioSettingsViewModel audioSettingsViewModel)
    {
        switch (mode)
        {
            case PresetMode.Standard:
                ApplyStandard();
                break;
            case PresetMode.Fast:
                ApplyLightweight();
                break;
            case PresetMode.Extreme:
                ApplyExtreme();
                break;
        }

        await audioSettingsViewModel.ReloadAudioDevice();
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