using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// LatencyGuideWindow.xaml 的交互逻辑
/// </summary>
public partial class LatencyGuideWindow : Window
{
    private readonly SharedViewModel _viewModel;
    private readonly int _offset;

    public LatencyGuideWindow()
    {
        InitializeComponent();
        DataContext = _viewModel = SharedViewModel.Instance;
        _offset = _viewModel.AppSettings?.RealtimeModeAudioOffset ?? 0;
        SharedViewModel.Instance.LatencyTestMode = true;
    }

    private void LatencyGuideWindow_OnClosed(object? sender, EventArgs e)
    {
        SharedViewModel.Instance.LatencyTestMode = false;
        if (DialogResult != true && _viewModel.AppSettings != null)
        {
            _viewModel.AppSettings.RealtimeModeAudioOffset = _offset;
        }
    }

    private void btnConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.AppSettings?.Save();
        DialogResult = true;
    }
}