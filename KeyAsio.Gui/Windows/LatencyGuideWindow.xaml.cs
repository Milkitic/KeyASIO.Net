using System;
using System.Windows;
using KeyAsio.Gui.UserControls;
using KeyAsio.Shared.Models;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// LatencyGuideWindow.xaml 的交互逻辑
/// </summary>
public partial class LatencyGuideWindow : DialogWindow
{
    private readonly SharedViewModel _viewModel;
    private readonly int _offset;

    public LatencyGuideWindow(SharedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _offset = _viewModel.AppSettings?.RealtimeOptions.RealtimeModeAudioOffset ?? 0;
        _viewModel.AutoMode = true;
    }

    private void LatencyGuideWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.AutoMode = false;
        if (DialogResult != true && _viewModel.AppSettings != null)
        {
            _viewModel.AppSettings.RealtimeOptions.RealtimeModeAudioOffset = _offset;
        }
    }

    private void btnConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.AppSettings?.Save();
        DialogResult = true;
    }
}