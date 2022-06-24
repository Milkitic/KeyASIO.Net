using System;
using System.Windows;
using KeyAsio.Gui.Models;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// OptionsWindow.xaml 的交互逻辑
/// </summary>
public partial class RealtimeOptionsWindow : Window
{
    private readonly SharedViewModel _viewModel;

    public RealtimeOptionsWindow()
    {
        InitializeComponent();
        DataContext = _viewModel = SharedViewModel.Instance;
    }

    private void RealtimeOptionsWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.AppSettings.Save();
    }
}