using System;
using System.ComponentModel;
using System.Text;
using Milki.Extensions.MouseKeyHook;

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

    public BindKeys IgnoreBeatmapHitsoundBindKey { get; set; } = BindKeys.Parse("Ctrl Z");

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
}

public class BindKeys
{
    private BindKeys(HookModifierKeys modifierKeys, HookKeys? keys)
    {
        ModifierKeys = modifierKeys;
        Keys = keys;
    }

    public HookModifierKeys ModifierKeys { get; }
    public HookKeys? Keys { get; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (ModifierKeys.HasFlag(HookModifierKeys.Control))
        {
            sb.Append("Ctrl");
        }

        if (ModifierKeys.HasFlag(HookModifierKeys.Shift))
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(" Shift");
        }

        if (ModifierKeys.HasFlag(HookModifierKeys.Alt))
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(" Alt");
        }

        if (Keys != null)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(Keys.ToString());
        }

        return sb.ToString();
    }

    public static BindKeys Parse(string str)
    {
        var modifierKeys = HookModifierKeys.None;
        HookKeys? keys = null;
        var split = str.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in split)
        {
            if (s.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            {
                modifierKeys |= HookModifierKeys.Control;
            }
            else if (s.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifierKeys |= HookModifierKeys.Shift;
            }
            else if (s.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifierKeys |= HookModifierKeys.Alt;
            }
            else if (keys != null)
            {
                keys = Enum.Parse<HookKeys>(s);
            }
        }

        return new BindKeys(modifierKeys, keys);
    }
}