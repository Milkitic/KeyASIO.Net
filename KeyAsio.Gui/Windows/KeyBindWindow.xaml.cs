using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Gui.Windows;

public class KeyBindWindowViewModel : ViewModelBase
{
    private ObservableCollection<HookKeys> _keys = new();

    public ObservableCollection<HookKeys> Keys
    {
        get => _keys;
        set => this.RaiseAndSetIfChanged(ref _keys, value);
    }
}

/// <summary>
/// KeyBindWindow.xaml 的交互逻辑
/// </summary>
public partial class KeyBindWindow : Window
{
    private readonly IKeyboardHook _keyboardHook;
    public KeyBindWindowViewModel ViewModel { get; }

    public KeyBindWindow(IEnumerable<HookKeys> hookKeys)
    {
        InitializeComponent();
        _keyboardHook = KeyboardHookFactory.CreateApplication();
        _keyboardHook.KeyPressed += keyboardHook_KeyPressed;
        DataContext = ViewModel = new KeyBindWindowViewModel();
        ViewModel.Keys = new ObservableCollection<HookKeys>(hookKeys);
    }

    private void keyboardHook_KeyPressed(HookModifierKeys hookModifierKeys, HookKeys hookKey, KeyAction type)
    {
        if (type == KeyAction.KeyDown && !ViewModel.Keys.Contains(hookKey))
        {
            Dispatcher.Invoke(() => ViewModel.Keys.Add(hookKey));
        }
    }

    private void KeyBindWindow_OnClosed(object? sender, EventArgs e)
    {
        _keyboardHook.Dispose();
        _keyboardHook.KeyPressed -= keyboardHook_KeyPressed;
    }

    private void btnConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}