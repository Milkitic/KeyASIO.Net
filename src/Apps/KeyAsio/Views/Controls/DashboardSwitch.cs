using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace KeyAsio.Views.Controls;

[PseudoClasses(":pro", ":corrupt", ":checked")]
public class DashboardSwitch : TemplatedControl
{
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<DashboardSwitch, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly StyledProperty<bool> IsProProperty =
        AvaloniaProperty.Register<DashboardSwitch, bool>(nameof(IsPro));

    public bool IsPro
    {
        get => GetValue(IsProProperty);
        set => SetValue(IsProProperty, value);
    }

    public static readonly StyledProperty<bool> IsCorruptProperty =
        AvaloniaProperty.Register<DashboardSwitch, bool>(nameof(IsCorrupt));

    public bool IsCorrupt
    {
        get => GetValue(IsCorruptProperty);
        set => SetValue(IsCorruptProperty, value);
    }

    public static readonly StyledProperty<string?> MixModeDisplayNameProperty =
        AvaloniaProperty.Register<DashboardSwitch, string?>(nameof(MixModeDisplayName));

    public string? MixModeDisplayName
    {
        get => GetValue(MixModeDisplayNameProperty);
        set => SetValue(MixModeDisplayNameProperty, value);
    }

    public static readonly StyledProperty<string?> MixModeTagProperty =
        AvaloniaProperty.Register<DashboardSwitch, string?>(nameof(MixModeTag));

    public string? MixModeTag
    {
        get => GetValue(MixModeTagProperty);
        set => SetValue(MixModeTagProperty, value);
    }

    public static readonly StyledProperty<bool> HasMixModeTagProperty =
        AvaloniaProperty.Register<DashboardSwitch, bool>(nameof(HasMixModeTag));

    public bool HasMixModeTag
    {
        get => GetValue(HasMixModeTagProperty);
        set => SetValue(HasMixModeTagProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsProProperty)
        {
            PseudoClasses.Set(":pro", change.GetNewValue<bool>());
        }
        else if (change.Property == IsCorruptProperty)
        {
            PseudoClasses.Set(":corrupt", change.GetNewValue<bool>());
        }
        else if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(":checked", change.GetNewValue<bool>());
        }
    }
}