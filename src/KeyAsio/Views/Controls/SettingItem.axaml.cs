using Avalonia;
using Avalonia.Controls;
using Material.Icons;

namespace KeyAsio.Views.Controls;

public partial class SettingItem : UserControl
{
    public SettingItem()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<MaterialIconKind> IconProperty =
        AvaloniaProperty.Register<SettingItem, MaterialIconKind>(nameof(Icon));

    public MaterialIconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<SettingItem, string>(nameof(Header));

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SettingItem, string>(nameof(Description));

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly StyledProperty<object> ActionContentProperty =
        AvaloniaProperty.Register<SettingItem, object>(nameof(ActionContent));

    public object ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}