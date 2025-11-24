using System;
using KeyAsio.Gui.UserControls;
using KeyAsio.Shared.Models;
using Milki.Extensions.Configuration;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// OptionsWindow.xaml 的交互逻辑
/// </summary>
public partial class RealtimeOptionsWindow : DialogWindow
{
    private readonly SharedViewModel _viewModel;

    public RealtimeOptionsWindow(SharedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private void RealtimeOptionsWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.AppSettings.Save();
    }
}