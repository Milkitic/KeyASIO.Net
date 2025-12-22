using System.ComponentModel;
using YamlDotNet.Serialization;

namespace KeyAsio.Shared.Models;

public class RealtimeOptions : ViewModelBase
{
    private bool _realtimeMode = true;
    private int _realtimeModeAudioOffset;
    private bool _ignoreBeatmapHitsound;
    private bool _ignoreStoryboardSamples;
    private bool _ignoreSliderTicksAndSlides;
    private SliderTailPlaybackBehavior _sliderTailPlaybackBehavior;
    private float _balanceFactor = 0.3f;
    private bool _ignoreComboBreak;
    private bool _ignoreLineVolumes;
    private bool _enableMusicFunctions;
    private int _musicTrackVolume = 100;
    private int _effectTrackVolume = 100;
    private bool _forceNightcoreBeats;
    private int _generalScanInterval = 50;
    private int _timingScanInterval = 15;

    [Description("If the set value is lower, the generic fields will be updated more promptly.\r\n" +
                 "This property is targeted at delay-insensitive fields and can be appropriately increased to reduce CPU usage.")]
    public int GeneralScanInterval
    {
        get => _generalScanInterval;
        set => SetField(ref _generalScanInterval, value);
    }

    [Description("If the set value is lower, the timing fields will be updated more promptly.\r\n" +
                 "This property is targeted at delay-sensitive field and best kept as low as possible.\r\n" +
                 "If you experience audio cutting issues, please increase the value appropriately.")]
    public int TimingScanInterval
    {
        get => _timingScanInterval;
        set => SetField(ref _timingScanInterval, value);
    }

    [Description("If enabled, the software will perform memory scanning and play the right hitsounds of beatmaps.")]
    public bool RealtimeMode
    {
        get => _realtimeMode;
        set => SetField(ref _realtimeMode, value);
    }

    [Description("[EXPERIMENTAL] If enabled, the software will enable music related functions.")]
    public bool EnableMusicFunctions
    {
        get => _enableMusicFunctions;
        set => SetField(ref _enableMusicFunctions, value);
    }

    [YamlIgnore]
    [Description("The offset when `RealtimeMode` is true (allow adjusting in GUI).")]
    public int RealtimeModeAudioOffset
    {
        get => _realtimeModeAudioOffset;
        set => SetField(ref _realtimeModeAudioOffset, value);
    }

    [Description("Ignore beatmap's hitsound and force using user skin instead.")]
    public bool IgnoreBeatmapHitsound
    {
        get => _ignoreBeatmapHitsound;
        set => SetField(ref _ignoreBeatmapHitsound, value);
    }

    [YamlIgnore]
    public BindKeys? IgnoreBeatmapHitsoundBindKey { get; set; }

    [Description("Ignore beatmap's storyboard samples.")]
    public bool IgnoreStoryboardSamples
    {
        get => _ignoreStoryboardSamples;
        set => SetField(ref _ignoreStoryboardSamples, value);
    }

    [YamlIgnore]
    public BindKeys? IgnoreStoryboardSamplesBindKey { get; set; }

    [Description("Ignore slider's ticks and slides.")]
    public bool IgnoreSliderTicksAndSlides
    {
        get => _ignoreSliderTicksAndSlides;
        set => SetField(ref _ignoreSliderTicksAndSlides, value);
    }

    [YamlIgnore]
    public BindKeys? IgnoreSliderTicksAndSlidesBindKey { get; set; }

    [Description("Slider tail's playback behavior. Normal: Force to play slider tail's sounds; KeepReverse: Play only if a slider with multiple reverses; Ignore: Ignore slider tail's sounds.")]
    public SliderTailPlaybackBehavior SliderTailPlaybackBehavior
    {
        get => _sliderTailPlaybackBehavior;
        set => SetField(ref _sliderTailPlaybackBehavior, value);
    }

    [Description("Balance factor.")]
    public float BalanceFactor
    {
        get => _balanceFactor;
        set => SetField(ref _balanceFactor, value);
    }

    [Description("Ignore combo break sound.")]
    public bool IgnoreComboBreak
    {
        get => _ignoreComboBreak;
        set => SetField(ref _ignoreComboBreak, value);
    }

    [Description("Ignore combo break sound.")]
    public bool IgnoreLineVolumes
    {
        get => _ignoreLineVolumes;
        set => SetField(ref _ignoreLineVolumes, value);
    }

    [Description("Music track volume.")]
    public int MusicTrackVolume
    {
        get => _musicTrackVolume;
        set => SetField(ref _musicTrackVolume, value);
    }

    [Description("Effect track volume.")]
    public int EffectTrackVolume
    {
        get => _effectTrackVolume;
        set => SetField(ref _effectTrackVolume, value);
    }

    [Description("Force to use nightcore beats.")]
    public bool ForceNightcoreBeats
    {
        get => _forceNightcoreBeats;
        set => SetField(ref _forceNightcoreBeats, value);
    }
}