using System;
using System.Collections.ObjectModel;
using System.Windows;
using KeyAsio.Gui.UserControls;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Gui.Windows;

public class KeyBindWindowViewModel : ViewModelBase
{
    private ObservableCollection<HookKeys> _keys = new();

    public ObservableCollection<HookKeys> Keys
    {
        get => _keys;
        set => SetField(ref _keys, value);
    }
}

/// <summary>
/// KeyBindWindow.xaml 的交互逻辑
/// </summary>
public partial class KeyBindWindow : DialogWindow
{
    private readonly IKeyboardHook _keyboardHook;
    public KeyBindWindowViewModel ViewModel { get; }

    public KeyBindWindow(YamlAppSettings appSettings)
    {
        InitializeComponent();
        _keyboardHook = appSettings.Input.UseRawInput
            ? KeyboardHookFactory.CreateRawInput()
            : KeyboardHookFactory.CreateApplication();
        _keyboardHook.KeyPressed += keyboardHook_KeyPressed;
        DataContext = ViewModel = new KeyBindWindowViewModel();
        ViewModel.Keys = new ObservableCollection<HookKeys>(appSettings.Input.Keys);
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