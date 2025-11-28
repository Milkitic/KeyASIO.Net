using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new NotSupportedException();
        }
        else
        {
            AppSettings = new AppSettings();
        }
    }

    public MainWindowViewModel(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }

    public AppSettings AppSettings { get; }

    public SliderTailPlaybackBehavior[] SliderTailBehaviors { get; } = Enum.GetValues<SliderTailPlaybackBehavior>();

    public string Greeting { get; } = "Welcome to Avalonia!";

    [ObservableProperty]
    public partial bool RealtimeMode { get; set; }

    [ObservableProperty]
    public partial double TargetBufferSize { get; set; } = 3;

    [ObservableProperty]
    public partial bool IsExclusiveMode { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLimiterEnabled { get; set; } = true;

    [RelayCommand]
    private void ApplyAudioSettings()
    {
        // TODO: Apply settings to the audio engine
    }
}