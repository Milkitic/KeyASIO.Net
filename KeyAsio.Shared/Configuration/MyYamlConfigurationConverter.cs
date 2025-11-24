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
        public bool UseRawInput { get; set; }
        public IEnumerable<HookKeys>? Keys { get; set; }
    }

    private class YamlPaths
    {
        public string? OsuFolderPath { get; set; }
        public string? HitsoundPath { get; set; }
        public string? SelectedSkinName { get; set; }
    }

    private class YamlAudio
    {
        public int SampleRate { get; set; }
        public DeviceDescription? PlaybackDevice { get; set; }
        public bool EnableLimiter { get; set; }
        public int MasterVolume { get; set; }
        public int MusicVolume { get; set; }
        public int EffectVolume { get; set; }
    }

    private class YamlLogging
    {
        public bool EnableDebugConsole { get; set; }
        public bool EnableErrorReporting { get; set; }
        public bool ErrorReportingConfirmed { get; set; }
    }

    private class YamlPerformance
    {
        public int AudioCacheThreadCount { get; set; }
    }

    private class YamlRealtime
    {
        public bool RealtimeMode { get; set; }
        public YamlRealtimeScanning? Scanning { get; set; }
        public YamlRealtimePlayback? Playback { get; set; }
        public YamlRealtimeFilters? Filters { get; set; }
    }

    private class YamlRealtimeScanning
    {
        public int GeneralInterval { get; set; }
        public int TimingInterval { get; set; }
    }

    private class YamlRealtimePlayback
    {
        public SliderTailPlaybackBehavior TailPlaybackBehavior { get; set; }
        public bool NightcoreBeats { get; set; }
        public float Balance { get; set; }
        public bool EnableMusicFunctions { get; set; }
    }

    private class YamlRealtimeFilters
    {
        public bool DisableBeatmapHitsounds { get; set; }
        public bool DisableStoryboardSamples { get; set; }
        public bool DisableSliderTicksAndSlides { get; set; }
        public bool DisableComboBreakSfx { get; set; }
        public bool IgnoreLineVolumes { get; set; }
    }
}