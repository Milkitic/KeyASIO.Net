using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace KeyAsio.Views.Controls;

[PseudoClasses(":active")]
public class DashboardSphere : TemplatedControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<DashboardSphere, bool>(nameof(IsActive));

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<DashboardSphere, string?>(nameof(StatusText));

    public string? StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly StyledProperty<int> ProcessIdProperty =
        AvaloniaProperty.Register<DashboardSphere, int>(nameof(ProcessId));

    public int ProcessId
    {
        get => GetValue(ProcessIdProperty);
        set => SetValue(ProcessIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":active", change.GetNewValue<bool>());
        }
    }
}
