using System.ComponentModel;
using KeyAsio.Audio;
using KeyAsio.Shared.Models;
using Milki.Extensions.Configuration.Converters;
using Milki.Extensions.MouseKeyHook;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KeyAsio.Shared.Configuration;

public class MyYamlConfigurationConverter : YamlConfigurationConverter
{
    private MyYamlConfigurationConverter()
    {
    }

    public static MyYamlConfigurationConverter Instance { get; } = new();

    protected override void ConfigSerializeBuilder(SerializerBuilder builder)
    {
        base.ConfigSerializeBuilder(builder);
        builder.WithTypeConverter(new BindKeysConverter());
    }

    protected override void ConfigDeserializeBuilder(DeserializerBuilder builder)
    {
        base.ConfigDeserializeBuilder(builder);
        builder.WithTypeConverter(new BindKeysConverter());
    }

    public override object DeserializeSettings(string content, Type type)
    {
        if (type == typeof(AppSettings))
        {
            if (!LooksLikeNewYaml(content))
            {
                return base.DeserializeSettings(content, type);
            }

            var builder = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .IgnoreFields()
                .WithTypeConverter(new BindKeysConverter());
            var deserializer = builder.Build();
            var yamlModel = deserializer.Deserialize<YamlAppSettings>(content);
            var app = FromYaml(yamlModel);
            return app;
        }

        return base.DeserializeSettings(content, type);
    }

    private static bool LooksLikeNewYaml(string content)
    {
        var tokens = new[] { "input", "paths", "audio", "logging", "performance", "realtime" };
        using var sr = new StringReader(content);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var span = line.AsSpan().TrimStart();
            foreach (var t in tokens)
            {
                if (span.StartsWith(t, StringComparison.OrdinalIgnoreCase))
                {
                    var idx = span.IndexOf(':');
                    if (idx == t.Length) return true;
                }
            }
        }
        return false;
    }

    public override string SerializeSettings(object obj)
    {
        if (obj is AppSettings s)
        {
            var yamlModel = ToYaml(s);
            var builder = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithEmissionPhaseObjectGraphVisitor(args => new DescriptionCommentsObjectGraphVisitor(args.InnerVisitor))
                .DisableAliases()
                .IgnoreFields()
                .WithTypeConverter(new BindKeysConverter());
            var converter = builder.Build();
            var content = converter.Serialize(yamlModel);
            return content;
        }

        return base.SerializeSettings(obj);
    }

    private static YamlAppSettings ToYaml(AppSettings s)
    {
        return new YamlAppSettings
        {
            Input = new YamlInput
            {
                UseRawInput = s.UseRawInput,
                Keys = s.Keys
            },
            Paths = new YamlPaths
            {
                OsuFolderPath = s.OsuFolder,
                HitsoundPath = s.HitsoundPath,
                SelectedSkinName = s.SelectedSkin
            },
            Audio = new YamlAudio
            {
                SampleRate = s.SampleRate,
                PlaybackDevice = s.Device,
                EnableLimiter = s.EnableLimiter,
                MasterVolume = (int)s.Volume,
                MusicVolume = s.RealtimeOptions.MusicTrackVolume,
                EffectVolume = s.RealtimeOptions.EffectTrackVolume
            },
            Logging = new YamlLogging
            {
                EnableDebugConsole = s.Debugging,
                EnableErrorReporting = s.SendLogsToDeveloper,
                ErrorReportingConfirmed = s.SendLogsToDeveloperConfirmed
            },
            Performance = new YamlPerformance
            {
                AudioCacheThreadCount = s.AudioCachingThreads
            },
            Realtime = new YamlRealtime
            {
                RealtimeMode = s.RealtimeOptions.RealtimeMode,
                Scanning = new YamlRealtimeScanning
                {
                    GeneralInterval = s.RealtimeOptions.GeneralScanInterval,
                    TimingInterval = s.RealtimeOptions.TimingScanInterval
                },
                Playback = new YamlRealtimePlayback
                {
                    TailPlaybackBehavior = s.RealtimeOptions.SliderTailPlaybackBehavior,
                    NightcoreBeats = s.RealtimeOptions.ForceNightcoreBeats,
                    Balance = s.RealtimeOptions.BalanceFactor,
                    EnableMusicFunctions = s.RealtimeOptions.EnableMusicFunctions,
                },
                Filters = new YamlRealtimeFilters
                {
                    DisableBeatmapHitsounds = s.RealtimeOptions.IgnoreBeatmapHitsound,
                    DisableStoryboardSamples = s.RealtimeOptions.IgnoreStoryboardSamples,
                    DisableSliderTicksAndSlides = s.RealtimeOptions.IgnoreSliderTicksAndSlides,
                    DisableComboBreakSfx = s.RealtimeOptions.IgnoreComboBreak,
                    IgnoreLineVolumes = s.RealtimeOptions.IgnoreLineVolumes
                }
            }
        };
    }

    private static AppSettings FromYaml(YamlAppSettings y)
    {
        var s = new AppSettings();
        if (y.Input != null)
        {
            s.UseRawInput = y.Input.UseRawInput;
            if (y.Input.Keys != null) s.Keys = y.Input.Keys.ToList();
        }
        if (y.Paths != null)
        {
            s.OsuFolder = y.Paths.OsuFolderPath;
            s.HitsoundPath = y.Paths.HitsoundPath ?? s.HitsoundPath;
            s.SelectedSkin = y.Paths.SelectedSkinName ?? s.SelectedSkin;
        }
        if (y.Audio != null)
        {
            s.SampleRate = y.Audio.SampleRate;
            s.Device = y.Audio.PlaybackDevice;
            s.EnableLimiter = y.Audio.EnableLimiter;
            s.Volume = y.Audio.MasterVolume;
            s.RealtimeOptions.MusicTrackVolume = y.Audio.MusicVolume;
            s.RealtimeOptions.EffectTrackVolume = y.Audio.EffectVolume;
        }
        if (y.Logging != null)
        {
            s.Debugging = y.Logging.EnableDebugConsole;
            s.SendLogsToDeveloper = y.Logging.EnableErrorReporting;
            s.SendLogsToDeveloperConfirmed = y.Logging.ErrorReportingConfirmed;
        }
        if (y.Performance != null)
        {
            s.AudioCachingThreads = y.Performance.AudioCacheThreadCount;
        }
        if (y.Realtime != null)
        {
            s.RealtimeOptions.RealtimeMode = y.Realtime.RealtimeMode;
            if (y.Realtime.Scanning != null)
            {
                s.RealtimeOptions.GeneralScanInterval = y.Realtime.Scanning.GeneralInterval;
                s.RealtimeOptions.TimingScanInterval = y.Realtime.Scanning.TimingInterval;
            }
            if (y.Realtime.Playback != null)
            {
                s.RealtimeOptions.SliderTailPlaybackBehavior = y.Realtime.Playback.TailPlaybackBehavior;
                s.RealtimeOptions.ForceNightcoreBeats = y.Realtime.Playback.NightcoreBeats;
                s.RealtimeOptions.BalanceFactor = y.Realtime.Playback.Balance;
                s.RealtimeOptions.EnableMusicFunctions = y.Realtime.Playback.EnableMusicFunctions;
            }
            if (y.Realtime.Filters != null)
            {
                s.RealtimeOptions.IgnoreBeatmapHitsound = y.Realtime.Filters.DisableBeatmapHitsounds;
                s.RealtimeOptions.IgnoreStoryboardSamples = y.Realtime.Filters.DisableStoryboardSamples;
                s.RealtimeOptions.IgnoreSliderTicksAndSlides = y.Realtime.Filters.DisableSliderTicksAndSlides;
                s.RealtimeOptions.IgnoreComboBreak = y.Realtime.Filters.DisableComboBreakSfx;
                s.RealtimeOptions.IgnoreLineVolumes = y.Realtime.Filters.IgnoreLineVolumes;
            }
        }
        return s;
    }

    private class YamlAppSettings
    {
        public YamlInput? Input { get; set; }
        public YamlPaths? Paths { get; set; }
        public YamlAudio? Audio { get; set; }
        public YamlLogging? Logging { get; set; }
        public YamlPerformance? Performance { get; set; }
        public YamlRealtime? Realtime { get; set; }
    }

    private class YamlInput
    {
        [Description("Use raw input for capturing keys; otherwise uses low‑level keyboard hook. Switch only if you encounter issues.")]
        public bool UseRawInput { get; set; }

        [Description("Trigger keys. Refer to https://docs.microsoft.com/en-us/dotnet/api/System.Windows.Forms.Keys.")]
        public IEnumerable<HookKeys>? Keys { get; set; }
    }

    private class YamlPaths
    {
        [Description("osu! folder. Usually auto-detected.")]
        public string? OsuFolderPath { get; set; }
        [Description("Default hitsound file path (relative or absolute).")]
        public string? HitsoundPath { get; set; }
        [Description("Skin used when realtime mode is enabled.")]
        public string? SelectedSkinName { get; set; }
    }

    private class YamlAudio
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

    private class YamlLogging
    {
        [Description("Enable console window for logs.")]
        public bool EnableDebugConsole { get; set; }
        [Description("Enable error/bug reporting to developer.")]
        public bool EnableErrorReporting { get; set; }
        public bool ErrorReportingConfirmed { get; set; }
    }

    private class YamlPerformance
    {
        [Description("Number of threads for audio caching.")]
        public int AudioCacheThreadCount { get; set; }
    }

    private class YamlRealtime
    {
        [Description("Enable memory scanning and correct hitsound playback.")]
        public bool RealtimeMode { get; set; }
        public YamlRealtimeScanning? Scanning { get; set; }
        public YamlRealtimePlayback? Playback { get; set; }
        public YamlRealtimeFilters? Filters { get; set; }
    }

    private class YamlRealtimeScanning
    {
        [Description("Lower values update generic fields more promptly. Intended for delay-insensitive fields; increase to reduce CPU usage.")]
        public int GeneralInterval { get; set; }
        [Description("Lower values update timing fields more promptly. Intended for delay‑sensitive fields; keep as low as possible. Increase if audio cutting occurs.")]
        public int TimingInterval { get; set; }
    }

    private class YamlRealtimePlayback
    {
        [Description("Slider‑tail playback behavior. Normal: always play; KeepReverse: play only on multi‑reverse sliders; Ignore: never play.")]
        public SliderTailPlaybackBehavior TailPlaybackBehavior { get; set; }
        [Description("Force use of nightcore beats.")]
        public bool NightcoreBeats { get; set; }
        [Description("Balance factor.")]
        public float Balance { get; set; }
        [Description("[Experimental] Enable music‑related functions.")]
        public bool EnableMusicFunctions { get; set; }
    }

    private class YamlRealtimeFilters
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
}