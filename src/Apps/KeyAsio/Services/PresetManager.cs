using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Lang;
using KeyAsio.ViewModels;
using Material.Icons;

namespace KeyAsio.Services;

public enum PresetMode
{
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
                PresetMode.Fast,
                SRKeys.Preset_Fast,
                SRKeys.Preset_FastDescription,
                MaterialIconKind.Feather,
                "#D01373"
            ),
            new PresetModel(
                PresetMode.Extreme,
                SRKeys.Preset_Extreme,
                SRKeys.Preset_ExtremeDescription,
                MaterialIconKind.RocketLaunch,
                "SukiDangerColor")
        ];
    }

    public PresetMode? GetCurrentPresetMode()
    {
        // Extreme
        if (_appSettings.Sync.Scanning.GeneralScanInterval == 50 &&
            _appSettings.Sync.Scanning.TimingScanInterval == 1 &&
            _appSettings.Sync.Playback.LimiterType == LimiterType.Off &&
            _appSettings.Sync.Playback.BalanceMode == BalanceMode.Off)
        {
            return PresetMode.Extreme;
        }

        // Fast
        if (_appSettings.Sync.Scanning.GeneralScanInterval == 50 &&
            _appSettings.Sync.Scanning.TimingScanInterval == 2 &&
            _appSettings.Sync.Playback.LimiterType == LimiterType.Peak &&
            _appSettings.Sync.Playback.BalanceMode == BalanceMode.ProMixFocus)
        {
            return PresetMode.Fast;
        }

        return null;
    }

    public async Task ApplyPreset(PresetMode mode, AudioSettingsViewModel audioSettingsViewModel)
    {
        switch (mode)
        {
            case PresetMode.Fast:
                ApplyLightweight();
                break;
            case PresetMode.Extreme:
                ApplyExtreme();
                break;
        }

        //await audioSettingsViewModel.ReloadAudioDevice();
    }

    private void ApplyLightweight()
    {
        _appSettings.Sync.Playback.LimiterType = LimiterType.Peak;
        _appSettings.Sync.Playback.BalanceMode = BalanceMode.ProMixFocus;

        _appSettings.Sync.Scanning.GeneralScanInterval = 50;
        _appSettings.Sync.Scanning.TimingScanInterval = 2;
    }

    private void ApplyExtreme()
    {
        _appSettings.Sync.Playback.LimiterType = LimiterType.Off;
        _appSettings.Sync.Playback.BalanceMode = BalanceMode.Off;

        _appSettings.Sync.Scanning.GeneralScanInterval = 50;
        _appSettings.Sync.Scanning.TimingScanInterval = 1;

        // todo: 平衡器算法、限频器算法、无视所有音量与声道变化等
    }
}
