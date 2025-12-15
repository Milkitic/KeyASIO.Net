using Avalonia;
using Avalonia.Controls;

namespace KeyAsio.Views.Controls;

public partial class StatBadge : UserControl
{
    public StatBadge()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatBadge, string>(nameof(Label));

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly StyledProperty<object> ValueProperty =
        AvaloniaProperty.Register<StatBadge, object>(nameof(Value));

    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}