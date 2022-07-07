using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeyAsio.Gui.UserControls;

public class CustomizableButton : Button
{
    protected FrameworkElement? HostContainer { get; private set; }

    public CustomizableButton()
    {
        Loaded += (_, _) =>
        {
            if (HostContainer != null)
            {
                return;
            }

            //HostWindow = Window.GetWindow(this);
            HostContainer = FindParentObjects(this, typeof(Page), typeof(Window));
        };

    }
    public ControlTemplate IconTemplate
    {
        get => (ControlTemplate)GetValue(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    public static readonly DependencyProperty IconTemplateProperty =
        DependencyProperty.Register(
            "IconTemplate",
            typeof(ControlTemplate),
            typeof(CustomizableButton),
            null
        );

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(CustomizableButton),
            new PropertyMetadata(new CornerRadius(0))
        );

    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    public static readonly DependencyProperty IconMarginProperty =
        DependencyProperty.Register(
            "IconMargin",
            typeof(Thickness),
            typeof(CustomizableButton),
            new PropertyMetadata(new Thickness(0, 0, 0, 0))
        );

    public Orientation IconOrientation
    {
        get => (Orientation)GetValue(IconOrientationProperty);
        set => SetValue(IconOrientationProperty, value);
    }

    public static readonly DependencyProperty IconOrientationProperty =
        DependencyProperty.Register(
            "IconOrientation",
            typeof(Orientation),
            typeof(CustomizableButton),
            new PropertyMetadata(Orientation.Horizontal)
        );

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            "IconSize",
            typeof(double),
            typeof(CustomizableButton),
            new PropertyMetadata(24d)
        );

    public Brush MouseOverBackground
    {
        get => (Brush)GetValue(MouseOverBackgroundProperty);
        set => SetValue(MouseOverBackgroundProperty, value);
    }

    public Brush MouseOverForeground
    {
        get => (Brush)GetValue(MouseOverForegroundProperty);
        set => SetValue(MouseOverForegroundProperty, value);
    }

    public Brush MouseDownBackground
    {
        get => (Brush)GetValue(MouseDownBackgroundProperty);
        set => SetValue(MouseDownBackgroundProperty, value);
    }

    public Brush MouseDownForeground
    {
        get => (Brush)GetValue(MouseDownForegroundProperty);
        set => SetValue(MouseDownForegroundProperty, value);
    }

    public Brush CheckedBackground
    {
        get => (Brush)GetValue(CheckedBackgroundProperty);
        set => SetValue(CheckedBackgroundProperty, value);
    }

    public Brush CheckedForeground
    {
        get => (Brush)GetValue(CheckedForegroundProperty);
        set => SetValue(CheckedForegroundProperty, value);
    }

    public static FrameworkElement? FindParentObjects(FrameworkElement obj, params Type[] types)
    {
        var parent = VisualTreeHelper.GetParent(obj);
        while (parent != null)
        {
            if (parent is FrameworkElement fe)
            {
                if (types.Length == 0)
                    return fe;

                var type = fe.GetType();
                if (types.Any(k => type.IsSubclassOf(k) || k == type))
                {
                    return fe;
                }
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    public static readonly DependencyProperty MouseOverBackgroundProperty = DependencyProperty.Register("MouseOverBackground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(default(Brush)));
    public static readonly DependencyProperty MouseOverForegroundProperty = DependencyProperty.Register("MouseOverForeground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(default(Brush)));
    public static readonly DependencyProperty MouseDownBackgroundProperty = DependencyProperty.Register("MouseDownBackground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(default(Brush)));
    public static readonly DependencyProperty MouseDownForegroundProperty = DependencyProperty.Register("MouseDownForeground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(default(Brush)));
    public static readonly DependencyProperty CheckedBackgroundProperty = DependencyProperty.Register("CheckedBackground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(default(Brush)));
    public static readonly DependencyProperty CheckedForegroundProperty = DependencyProperty.Register("CheckedForeground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(default(Brush)));

    static CustomizableButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomizableButton), new FrameworkPropertyMetadata(typeof(CustomizableButton)));
    }
}