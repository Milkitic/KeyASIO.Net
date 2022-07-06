using System.ComponentModel;

namespace KeyAsio.Gui.Models;

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
    private bool _disableMusicFunctions;
    private int _musicTrackVolume = 100;
    private int _effectTrackVolume = 100;

    [Description("If true, the software will enable memory scanning and play the right hitsounds of beatmaps.")]
    public bool RealtimeMode
    {
        get => _realtimeMode;
        set => this.RaiseAndSetIfChanged(ref _realtimeMode, value);
    }

    [Description("If true, the software will disable music related functions.")]
    public bool DisableMusicFunctions
    {
        get => _disableMusicFunctions;
        set => this.RaiseAndSetIfChanged(ref _disableMusicFunctions, value);
    }

    [Description("The offset when `RealtimeMode` is true (allow adjusting in GUI).")]
    public int RealtimeModeAudioOffset
    {
        get => _realtimeModeAudioOffset;
        set => this.RaiseAndSetIfChanged(ref _realtimeModeAudioOffset, value);
    }

    [Description("Ignore beatmap's hitsound and force using user skin instead.")]
    public bool IgnoreBeatmapHitsound
    {
        get => _ignoreBeatmapHitsound;
        set => this.RaiseAndSetIfChanged(ref _ignoreBeatmapHitsound, value);
    }

    public BindKeys IgnoreBeatmapHitsoundBindKey { get; set; } = BindKeys.Parse("Ctrl+Q");

    [Description("Ignore beatmap's storyboard samples.")]
    public bool IgnoreStoryboardSamples
    {
        get => _ignoreStoryboardSamples;
        set => this.RaiseAndSetIfChanged(ref _ignoreStoryboardSamples, value);
    }

    public BindKeys IgnoreStoryboardSamplesBindKey { get; set; } = BindKeys.Parse("Ctrl+W");

    [Description("Ignore slider's ticks and slides.")]
    public bool IgnoreSliderTicksAndSlides
    {
        get => _ignoreSliderTicksAndSlides;
        set => this.RaiseAndSetIfChanged(ref _ignoreSliderTicksAndSlides, value);
    }

    public BindKeys IgnoreSliderTicksAndSlidesBindKey { get; set; } = BindKeys.Parse("Ctrl+E");

    [Description("Slider tail's playback behavior. Normal: Force to play slider tail's sounds; KeepReverse: Play only if a slider with multiple reverses; Ignore: Ignore slider tail's sounds.")]
    public SliderTailPlaybackBehavior SliderTailPlaybackBehavior
    {
        get => _sliderTailPlaybackBehavior;
        set => this.RaiseAndSetIfChanged(ref _sliderTailPlaybackBehavior, value);
    }

    [Description("Balance factor.")]
    public float BalanceFactor
    {
        get => _balanceFactor;
        set => this.RaiseAndSetIfChanged(ref _balanceFactor, value);
    }

    [Description("Ignore combo break sound.")]
    public bool IgnoreComboBreak
    {
        get => _ignoreComboBreak;
        set => this.RaiseAndSetIfChanged(ref _ignoreComboBreak, value);
    }

    [Description("Ignore combo break sound.")]
    public bool IgnoreLineVolumes
    {
        get => _ignoreLineVolumes;
        set => this.RaiseAndSetIfChanged(ref _ignoreLineVolumes, value);
    }

    [Description("Music track volume.")]
    public int MusicTrackVolume
    {
        get => _musicTrackVolume;
        set => this.RaiseAndSetIfChanged(ref _musicTrackVolume, value);
    }

    [Description("Effect track volume.")]
    public int EffectTrackVolume
    {
        get => _effectTrackVolume;
        set => this.RaiseAndSetIfChanged(ref _effectTrackVolume, value);
    }
}