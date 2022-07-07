using System;
using System.Windows;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.UserControls;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// LatencyGuideWindow.xaml 的交互逻辑
/// </summary>
public partial class LatencyGuideWindow : DialogWindow
{
    private readonly SharedViewModel _viewModel;
    private readonly int _offset;

    public LatencyGuideWindow()
    {
        InitializeComponent();
        DataContext = _viewModel = SharedViewModel.Instance;
        _offset = _viewModel.AppSettings?.RealtimeOptions.RealtimeModeAudioOffset ?? 0;
        SharedViewModel.Instance.LatencyTestMode = true;
    }

    private void LatencyGuideWindow_OnClosed(object? sender, EventArgs e)
    {
        SharedViewModel.Instance.LatencyTestMode = false;
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