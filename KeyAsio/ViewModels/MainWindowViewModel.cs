using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    [ObservableProperty]
    public partial bool RealtimeMode { get; set; }
}