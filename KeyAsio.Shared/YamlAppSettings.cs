using System.ComponentModel;
using KeyAsio.Audio;
using KeyAsio.Shared.Models;
using Milki.Extensions.Configuration;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Shared;

public class YamlAppSettings : IConfigurationBase
{
    public YamlInput Input { get => field ??= new(); init; }
    public YamlPaths Paths { get => field ??= new(); init; }
    public YamlAudio Audio { get => field ??= new(); init; }
    public YamlLogging Logging { get => field ??= new(); init; }
    public YamlPerformance Performance { get => field ??= new(); init; }
    public YamlRealtime Realtime { get => field ??= new(); init; }
}

public partial class YamlInput : INotifyPropertyChanged
{
    [Description("Use raw input for capturing keys; otherwise uses low‑level keyboard hook. Switch only if you encounter issues.")]
    public bool UseRawInput { get; set; }

    [Description("Trigger keys. Refer to https://docs.microsoft.com/en-us/dotnet/api/System.Windows.Forms.Keys.")]
    public List<HookKeys> Keys { get; set; } = [HookKeys.Z, HookKeys.X];
}

public partial class YamlPaths : INotifyPropertyChanged
{
    [Description("osu! folder. Usually auto-detected.")]
    public string? OsuFolderPath { get; set; }
    [Description("Default hitsound file path (relative or absolute).")]
    public string? HitsoundPath { get; set; }
    [Description("Skin used when realtime mode is enabled.")]
    public string? SelectedSkinName { get; set; }
}

public partial class YamlAudio : INotifyPropertyChanged
{
    [Description("Output sample rate (adjustable in GUI).")]
    public int SampleRate { get; set; }
    [Description("Playback device configuration (configure in GUI).")]
    public DeviceDescription? PlaybackDevice { get; set; }
    [Description("Enable limiter to reduce clipping/distortion; disabling yields unprocessed signal. Useful at high master volume.")]
    public bool EnableLimiter { get; set; }
    [Description("Master volume. Range: 0–150.")]
    public int MasterVolume { get; set; }
    [Description("Music track volume.")]
    public int MusicVolume { get; set; }
    [Description("Effect track volume.")]
    public int EffectVolume { get; set; }
}

public partial class YamlLogging : INotifyPropertyChanged
{
    [Description("Enable console window for logs.")]
    public bool EnableDebugConsole { get; set; }
    [Description("Enable error/bug reporting to developer.")]
    public bool EnableErrorReporting { get; set; }
    public bool ErrorReportingConfirmed { get; set; }
    public string? PlayerBase64 { get; set; }
}

public partial class YamlPerformance : INotifyPropertyChanged
{
    [Description("Number of threads for audio caching.")]
    public int AudioCacheThreadCount { get; set; }
}

public partial class YamlRealtime : INotifyPropertyChanged
{
    [Description("Enable memory scanning and correct hitsound playback.")]
    public bool RealtimeMode { get; set; }
    [Description("[Experimental] Enable music‑related functions.")]
    public bool RealtimeEnableMusic { get; set; }
    public YamlRealtimeScanning Scanning { get => field ??= new(); init; }
    public YamlRealtimePlayback Playback { get => field ??= new(); init; }
    public YamlRealtimeFilters Filters { get => field ??= new(); init; }
}

public partial class YamlRealtimeScanning : INotifyPropertyChanged
{
    [Description("Lower values update generic fields more promptly. Intended for delay-insensitive fields; increase to reduce CPU usage.")]
    public int GeneralInterval { get; set; }
    [Description("Lower values update timing fields more promptly. Intended for delay‑sensitive fields; keep as low as possible. Increase if audio cutting occurs.")]
    public int TimingInterval { get; set; }
}

public partial class YamlRealtimePlayback : INotifyPropertyChanged
{
    [Description("Slider‑tail playback behavior. Normal: always play; KeepReverse: play only on multi‑reverse sliders; Ignore: never play.")]
    public SliderTailPlaybackBehavior TailPlaybackBehavior { get; set; }
    [Description("Force use of nightcore beats.")]
    public bool NightcoreBeats { get; set; }
    [Description("Balance factor.")]
    public float BalanceFactor { get; set; }
}

public partial class YamlRealtimeFilters : INotifyPropertyChanged
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