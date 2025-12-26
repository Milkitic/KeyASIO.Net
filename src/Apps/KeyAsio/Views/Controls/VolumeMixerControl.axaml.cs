using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace KeyAsio.Views.Controls;

public partial class VolumeMixerControl : UserControl
{
    public VolumeMixerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Slider_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is Slider slider)
        {
            slider.Value += e.Delta.Y;
            e.Handled = true;
        }
    }
}