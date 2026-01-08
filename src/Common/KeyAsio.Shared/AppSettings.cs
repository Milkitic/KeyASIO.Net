using System.ComponentModel;
using KeyAsio.Core.Audio;
using KeyAsio.Shared.Models;
using Milki.Extensions.Configuration;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Shared;

public class AppSettings : IConfigurationBase
{
    public AppSettingsGeneral General { get => field ??= new(); init; }
    public AppSettingsInput Input { get => field ??= new(); init; }
    public AppSettingsPaths Paths { get => field ??= new(); init; }
    public AppSettingsAudio Audio { get => field ??= new(); init; }
    public AppSettingsLogging Logging { get => field ??= new(); init; }
    public AppSettingsPerformance Performance { get => field ??= new(); init; }
    public AppSettingsSync Sync { get => field ??= new(); init; }
}

public partial class AppSettingsGeneral : INotifyPropertyChanged
{
    [Description("Allow multiple instances of the application to run simultaneously.")]
    public bool AllowMultipleInstance { get; set; }

    [Description("Application language.")]
    public string? Language { get; set; }

    [Description("Application theme.")]
    public AppTheme Theme { get; set; } = AppTheme.System;
}

public partial class AppSettingsInput : INotifyPropertyChanged
{
    [Description("Use raw input for capturing keys; otherwise uses low‑level keyboard hook. " +
                 "Switch only if you encounter issues.")]
    public bool UseRawInput { get; set; } = true;

    [Description("Trigger keys for osu! standard mode. Refer to https://docs.microsoft.com/en-us/dotnet/api/System.Windows.Forms.Keys.")]
    public List<HookKeys> OsuKeys { get; set; } = [HookKeys.Z, HookKeys.X];

    [Description("Trigger keys for osu!taiko mode.")]
    public List<HookKeys> TaikoKeys { get; set; } = [HookKeys.Z, HookKeys.X, HookKeys.C, HookKeys.V];

    [Description("Trigger keys for osu!catch mode.")]
    public List<HookKeys> CatchKeys { get; set; } = [HookKeys.Shift, HookKeys.Left, HookKeys.Right];

    [Description("Trigger keys for osu!mania mode (4k-10k). Key is key count, Value is list of keys.")]
    public Dictionary<int, List<HookKeys>> ManiaKeys { get; set; } = new()
    {
        [4] = [HookKeys.D, HookKeys.F, HookKeys.J, HookKeys.K],
        [5] = [HookKeys.D, HookKeys.F, HookKeys.Space, HookKeys.J, HookKeys.K],
        [6] = [HookKeys.S, HookKeys.D, HookKeys.F, HookKeys.J, HookKeys.K, HookKeys.L],
        [7] = [HookKeys.S, HookKeys.D, HookKeys.F, HookKeys.Space, HookKeys.J, HookKeys.K, HookKeys.L],
        [8] = [HookKeys.A, HookKeys.S, HookKeys.D, HookKeys.F, HookKeys.J, HookKeys.K, HookKeys.L, HookKeys.OemSemicolon],
        [9] = [HookKeys.A, HookKeys.S, HookKeys.D, HookKeys.F, HookKeys.Space, HookKeys.J, HookKeys.K, HookKeys.L, HookKeys.OemSemicolon],
        [10] = [HookKeys.Q, HookKeys.W, HookKeys.E, HookKeys.R, HookKeys.V, HookKeys.N, HookKeys.U, HookKeys.I, HookKeys.O, HookKeys.P]
    };
}

public partial class AppSettingsPaths : INotifyPropertyChanged
{
    [Description("osu! folder. Usually auto-detected.")]
    public string? OsuFolderPath { get; set; } = "";

    [Description("Default hitsound file path (relative or absolute).")]
    public string? HitsoundPath { get; set; } = "./resources/default/normal-hitnormal.ogg";

    [Description("Skin used when sync mode is enabled.")]
    public string? SelectedSkinName { get; set; }

    [Description("Allow automatic loading of skins from osu! folder.")]
    public bool? AllowAutoLoadSkins { get; set; }
}

public partial class AppSettingsAudio : INotifyPropertyChanged
{
    [Description("Output sample rate (adjustable in GUI).")]
    public int SampleRate { get; set; } = 48000;

    [Description("Playback device configuration (configure in GUI).")]
    public DeviceDescription? PlaybackDevice { get; set; }

    [Description("Prevents distortion when multiple hitsounds stack (e.g. during streams). " +
                 "Disable to preserve raw dynamic range.")]
    // 对于想要所听即所得的用户，建议关闭。
    public bool EnableLimiter { get; set; } = true;

    [Description("Master volume. Range: 0–150. " +
                 "For values above 100, consider disabling the Limiter to avoid aggressive compression.")]
    public int MasterVolume { get; set; } = 50;

    [Description("Music track volume.")]
    public int MusicVolume { get; set; } = 100;

    [Description("Effect track volume.")]
    public int EffectVolume { get; set; } = 100;

    [Description("Extend the maximum volume limit to 150%.")]
    public bool EnableExtendedVolume { get; set; }
}

public partial class AppSettingsLogging : INotifyPropertyChanged
{
    [Description("Enable console window for logs.")]
    public bool EnableDebugConsole { get; set; }

    [Description("Enable error/bug reporting to developer.")]
    public bool? EnableErrorReporting { get; set; }

    public string? PlayerBase64 { get; set; }
}

public partial class AppSettingsPerformance : INotifyPropertyChanged
{
    [Description("Accelerates processing using AVX-512. " +
                 "Disable on older Intel CPUs (pre-11th Gen) to avoid clock speed throttling.")]
    public bool EnableAvx512 { get; set; } = true;
}

public partial class AppSettingsSync : INotifyPropertyChanged
{
    [Description("Enable memory scanning and correct hitsound playback.")]
    public bool EnableSync { get; set; } = true;

    [Description("[Experimental] Enable music‑related functions.")]
    public bool EnableMixSync { get; set; }

    public AppSettingsSyncScanning Scanning { get => field ??= new(); init; }
    public AppSettingsSyncPlayback Playback { get => field ??= new(); init; }
    public AppSettingsSyncFilters Filters { get => field ??= new(); init; }
}

public partial class AppSettingsSyncScanning : INotifyPropertyChanged
{
    [Description("Lower values update generic fields more promptly. " +
                 "Intended for delay-insensitive fields; increase to reduce CPU usage.")]
    public int GeneralScanInterval { get; set; } = 50;

    [Description("Lower values update timing fields more promptly. " +
                 "Intended for delay‑sensitive fields; keep as low as possible. " +
                 "Increase if audio cutting occurs.")]
    public int TimingScanInterval { get; set; } = 2;
}

public partial class AppSettingsSyncPlayback : INotifyPropertyChanged
{
    [Description("Slider‑tail playback behavior. " +
                 "Normal: always play; KeepReverse: play only on multi‑reverse sliders; Ignore: never play.")]
    public SliderTailPlaybackBehavior TailPlaybackBehavior { get; set; } = SliderTailPlaybackBehavior.Normal;

    [Description("Force use of nightcore beats.")]
    public bool NightcoreBeats { get; set; }

    [Description("Balance factor.")]
    public float BalanceFactor { get; set; } = 0.6666667f;
}

public partial class AppSettingsSyncFilters : INotifyPropertyChanged
{
    [Description("Ignore beatmap hitsounds and use user skin instead.")]
    public bool DisableBeatmapHitsounds { get; set; }

    [Description("Ignore beatmap storyboard samples.")]
    public bool DisableStoryboardSamples { get; set; }

    [Description("Ignore slider ticks and slides.")]
    public bool DisableSliderTicksAndSlides { get; set; }

    [Description("Ignore combo break sound.")]
    public bool DisableComboBreakSfx { get; set; }

    [Description("Ignore beatmap line volume changes.")]
    public bool IgnoreLineVolumes { get; set; }
}