using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace KeyAsio.Gui.UserControls;

public class WindowFrame : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(nameof(Child),
        typeof(object),
        typeof(WindowFrame),
        new PropertyMetadata(default(object)));

    public static readonly DependencyProperty IsMaxProperty = DependencyProperty.Register(nameof(IsMax),
        typeof(bool),
        typeof(WindowFrame),
        new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty HasMinProperty = DependencyProperty.Register(nameof(HasMin),
        typeof(bool),
        typeof(WindowFrame),
        new PropertyMetadata(true));

    public static readonly DependencyProperty HasMaxProperty = DependencyProperty.Register(nameof(HasMax),
        typeof(bool),
        typeof(WindowFrame),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CanCloseProperty = DependencyProperty.Register(nameof(CanClose),
        typeof(bool),
        typeof(WindowFrame),
        new PropertyMetadata(true));

    public static readonly DependencyProperty HasIconProperty = DependencyProperty.Register(nameof(HasIcon),
        typeof(bool),
        typeof(WindowFrame),
        new PropertyMetadata(true));

    private Window? _owner;

    public bool CanClose
    {
        get => (bool)GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public bool HasMin
    {
        get => (bool)GetValue(HasMinProperty);
        set => SetValue(HasMinProperty, value);
    }

    public bool HasMax
    {
        get => (bool)GetValue(HasMaxProperty);
        set => SetValue(HasMaxProperty, value);
    }

    public bool IsMax
    {
        get => (bool)GetValue(IsMaxProperty);
        set => SetValue(IsMaxProperty, value);
    }

    public object Child
    {
        get => GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    public bool HasIcon
    {
        get => (bool)GetValue(HasIconProperty);
        set => SetValue(HasIconProperty, value);
    }

    public Window? Owner
    {
        get => _owner;
        internal set => SetField(ref _owner, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}