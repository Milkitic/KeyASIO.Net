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
        if (type == typeof(YamlAppSettings))
        {
            if (!LooksLikeNewYaml(content))
            {
                return ToYaml((AppSettings)base.DeserializeSettings(content, typeof(AppSettings)));
            }

            var builder = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .IgnoreFields()
                .WithTypeConverter(new BindKeysConverter());
            var deserializer = builder.Build();
            var yamlModel = deserializer.Deserialize<YamlAppSettings>(content);
            return yamlModel;
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
        if (obj is YamlAppSettings yamlModel)
        {
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
                ErrorReportingConfirmed = s.SendLogsToDeveloperConfirmed,
                PlayerBase64 = s.PlayerBase64
            },
            Performance = new YamlPerformance
            {
                AudioCacheThreadCount = s.AudioCachingThreads
            },
            Realtime = new YamlRealtime
            {
                RealtimeMode = s.RealtimeOptions.RealtimeMode,
                RealtimeEnableMusic = s.RealtimeOptions.EnableMusicFunctions,
                Scanning = new YamlRealtimeScanning
                {
                    GeneralInterval = s.RealtimeOptions.GeneralScanInterval,
                    TimingInterval = s.RealtimeOptions.TimingScanInterval
                },
                Playback = new YamlRealtimePlayback
                {
                    TailPlaybackBehavior = s.RealtimeOptions.SliderTailPlaybackBehavior,
                    NightcoreBeats = s.RealtimeOptions.ForceNightcoreBeats,
                    BalanceFactor = s.RealtimeOptions.BalanceFactor,
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
            s.PlayerBase64 = y.Logging.PlayerBase64 ?? "";
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
                s.RealtimeOptions.BalanceFactor = y.Realtime.Playback.BalanceFactor;
                s.RealtimeOptions.EnableMusicFunctions = y.Realtime.RealtimeEnableMusic;
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
}