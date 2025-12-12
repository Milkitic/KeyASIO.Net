using System.Diagnostics.CodeAnalysis;
using Milki.Extensions.Configuration.Converters;
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

    public override object DeserializeSettings(string content,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (type == typeof(AppSettings))
        {
            if (!LooksLikeNewYaml(content))
            {
                return ToYaml((LegacyAppSettings)base.DeserializeSettings(content, typeof(LegacyAppSettings)));
            }

            var builder = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .IgnoreFields()
                .WithTypeConverter(new BindKeysConverter());
            var deserializer = builder.Build();
            var yamlModel = deserializer.Deserialize<AppSettings>(content);
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
        if (obj is AppSettings yamlModel)
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

    private static AppSettings ToYaml(LegacyAppSettings s)
    {
        return new AppSettings
        {
            Input = new AppSettingsInput
            {
                UseRawInput = s.UseRawInput,
                Keys = s.Keys
            },
            Paths = new AppSettingsPaths
            {
                OsuFolderPath = s.OsuFolder,
                HitsoundPath = s.HitsoundPath,
                SelectedSkinName = s.SelectedSkin
            },
            Audio = new AppSettingsAudio
            {
                SampleRate = s.SampleRate,
                PlaybackDevice = s.Device,
                EnableLimiter = s.EnableLimiter,
                MasterVolume = (int)s.Volume,
                MusicVolume = s.RealtimeOptions.MusicTrackVolume,
                EffectVolume = s.RealtimeOptions.EffectTrackVolume
            },
            Logging = new AppSettingsLogging
            {
                EnableDebugConsole = s.Debugging,
                EnableErrorReporting = s.SendLogsToDeveloperConfirmed ? s.SendLogsToDeveloper : null,
                PlayerBase64 = s.PlayerBase64
            },
            Performance = new AppSettingsPerformance
            {
                AudioCacheThreadCount = s.AudioCachingThreads
            },
            Realtime = new AppSettingsRealtime
            {
                RealtimeMode = s.RealtimeOptions.RealtimeMode,
                RealtimeEnableMusic = s.RealtimeOptions.EnableMusicFunctions,
                Scanning = new AppSettingsRealtimeScanning
                {
                    GeneralScanInterval = s.RealtimeOptions.GeneralScanInterval,
                    TimingScanInterval = s.RealtimeOptions.TimingScanInterval
                },
                Playback = new AppSettingsRealtimePlayback
                {
                    TailPlaybackBehavior = s.RealtimeOptions.SliderTailPlaybackBehavior,
                    NightcoreBeats = s.RealtimeOptions.ForceNightcoreBeats,
                    BalanceFactor = s.RealtimeOptions.BalanceFactor,
                },
                Filters = new AppSettingsRealtimeFilters
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

    private static LegacyAppSettings FromYaml(AppSettings y)
    {
        var s = new LegacyAppSettings();
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
            s.SendLogsToDeveloper = y.Logging.EnableErrorReporting ?? true;
            s.SendLogsToDeveloperConfirmed = y.Logging.EnableErrorReporting.HasValue;
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
                s.RealtimeOptions.GeneralScanInterval = y.Realtime.Scanning.GeneralScanInterval;
                s.RealtimeOptions.TimingScanInterval = y.Realtime.Scanning.TimingScanInterval;
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