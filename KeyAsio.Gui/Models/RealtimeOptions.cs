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

    [Description("If true, the software will enable memory scanning and play the right hitsounds of beatmaps.")]
    public bool RealtimeMode
    {
        get => _realtimeMode;
        set => this.RaiseAndSetIfChanged(ref _realtimeMode, value);
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

    [Description("Ignore beatmap's storyboard samples.")]
    public bool IgnoreStoryboardSamples
    {
        get => _ignoreStoryboardSamples;
        set => this.RaiseAndSetIfChanged(ref _ignoreStoryboardSamples, value);
    }

    [Description("Ignore slider's ticks and slides.")]
    public bool IgnoreSliderTicksAndSlides
    {
        get => _ignoreSliderTicksAndSlides;
        set => this.RaiseAndSetIfChanged(ref _ignoreSliderTicksAndSlides, value);
    }

    [Description("Slider tail's playback behavior. Normal: Force to play sider tail's sounds; KeepReverse: Play only if a slider with multiple reverses; Ignore: Ignore slider tail's sounds.")]
    public SliderTailPlaybackBehavior SliderTailPlaybackBehavior
    {
        get => _sliderTailPlaybackBehavior;
        set => this.RaiseAndSetIfChanged(ref _sliderTailPlaybackBehavior, value);
    }
}