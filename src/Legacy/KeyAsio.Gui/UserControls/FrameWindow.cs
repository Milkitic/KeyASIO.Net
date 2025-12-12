using System;
using System.Windows;

namespace KeyAsio.Gui.UserControls;

public class FrameWindow : Window
{
    public static readonly DependencyProperty CanCloseProperty = DependencyProperty.Register(nameof(CanClose),
        typeof(bool),
        typeof(FrameWindow),
        new PropertyMetadata(true, (d, e) =>
        {
            if (d is FrameWindow ex) ex.OnCanCloseChanged(ex, new RoutedEventArgs());
        }));

    protected readonly WindowFrame? Frame;

    public FrameWindow(WindowFrame frame)
    {
        Frame = frame;
        Frame.Owner = this;
        Initialized += FrameWindow_Initialized;
        StateChanged += FrameWindow_StateChanged;
    }

    public bool CanClose
    {
        get => (bool)GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    protected virtual void OnCanCloseChanged(object? sender, RoutedEventArgs e)
    {
        if (Frame != null) Frame.CanClose = CanClose;
    }

    private void FrameWindow_StateChanged(object? sender, EventArgs e)
    {
        if (Frame == null) return;
        if (WindowState == WindowState.Normal)
        {
            Frame.IsMax = false;
        }
        else if (WindowState == WindowState.Maximized)
        {
            Frame.IsMax = true;
        }
    }

    private void FrameWindow_Initialized(object? sender, EventArgs e)
    {
        var oldContent = Content;
        if (Frame != null)
        {
            Frame.Child = oldContent;
            Content = null;
            Content = Frame;
        }

        SwitchWindowStyle();
    }

    private void SwitchWindowStyle()
    {
        if (Frame == null) return;
        if (WindowStyle == WindowStyle.ToolWindow)
        {
            Frame.HasMax = false;
            Frame.HasMin = false;
            Frame.HasIcon = false;
        }
        else if (ResizeMode == ResizeMode.NoResize)
        {
            Frame.HasMax = false;
            Frame.HasMin = false;
            Frame.HasIcon = Icon is not null;
        }
    }
}