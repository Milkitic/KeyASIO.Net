using System.ComponentModel;
using Avalonia.Controls;
using KeyAsio.ViewModels;

namespace KeyAsio.Views.Pages;

public partial class AudioEnginePage : UserControl
{
    private MainWindowViewModel? _viewModel;

    public AudioEnginePage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.AudioSettings.PropertyChanged -= AudioSettings_PropertyChanged;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            _viewModel = vm;
            _viewModel.AudioSettings.PropertyChanged += AudioSettings_PropertyChanged;
            UpdateSeverity();
        }
        else
        {
            _viewModel = null;
        }
    }

    private void AudioSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioSettingsViewModel.InfoBarSeverity))
        {
            UpdateSeverity();
        }
    }

    private void UpdateSeverity()
    {
        if (_viewModel != null)
        {
            AudioInfoBar.Severity = _viewModel.AudioSettings.InfoBarSeverity;
        }
    }
}