using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Utils;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using NAudio.Wave;
using Window = System.Windows.Window;

namespace KeyAsio.Gui;

public class MainWindowViewModel : ViewModelBase
{
    private AudioPlaybackEngine? _audioPlaybackEngine;
    private DeviceDescription? _deviceDescription;
    private AppSettings? _appSettings;

    public AudioPlaybackEngine? AudioPlaybackEngine
    {
        get => _audioPlaybackEngine;
        set => this.RaiseAndSetIfChanged(ref _audioPlaybackEngine, value);
    }

    public DeviceDescription? DeviceDescription
    {
        get => _deviceDescription;
        set => this.RaiseAndSetIfChanged(ref _deviceDescription, value);
    }

    public AppSettings? AppSettings
    {
        get => _appSettings;
        set => this.RaiseAndSetIfChanged(ref _appSettings, value);
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _forceClose;
    private readonly AppSettings _appSettings;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel = new MainWindowViewModel();
        _appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();
        _viewModel.AppSettings = _appSettings;
    }

    private void SelectDevice()
    {
        var window = new DeviceWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (window.ShowDialog() != true) return;

        var deviceDescription = window.ViewModel.SelectedDevice;
        if (deviceDescription == null) return;

        DisposeDevice(false);

        var latency = window.ViewModel.Latency;
        var isExclusive = window.ViewModel.IsExclusive;
        deviceDescription.Latency = latency;
        deviceDescription.IsExclusive = isExclusive;
        _appSettings.SampleRate = window.ViewModel.SampleRate;

        LoadDevice(deviceDescription, true);
    }

    private void LoadDevice(DeviceDescription deviceDescription, bool saveToSettings)
    {
        try
        {
            var device = DeviceCreationHelper.CreateDevice(out var actualDescription, deviceDescription);
            _viewModel.AudioPlaybackEngine = new AudioPlaybackEngine(device,
                _appSettings.SampleRate, _appSettings.Channels,
                notifyProgress: false, enableVolume: _appSettings.VolumeEnabled)
            {
                Volume = _appSettings.Volume
            };

            if (device is AsioOut asioOut)
            {
                asioOut.DriverResetRequest += AsioOut_DriverResetRequest;
            }

            _viewModel.DeviceDescription = actualDescription;
            if (saveToSettings)
            {
                _appSettings.Device = actualDescription;
                _appSettings.Save();
            }
        }
        catch (Exception ex)
        {
            Growl.Error("Error while creating device:\r\n" + ex.ToSimpleTypeMessage());
        }
    }

    private void DisposeDevice(bool saveToSettings)
    {
        if (_viewModel.AudioPlaybackEngine == null) return;

        if (_viewModel.AudioPlaybackEngine.OutputDevice is AsioOut asioOut)
        {
            asioOut.DriverResetRequest -= AsioOut_DriverResetRequest;
        }

        _viewModel.AudioPlaybackEngine.OutputDevice?.Dispose();
        _viewModel.AudioPlaybackEngine = null;
        _viewModel.DeviceDescription = null;

        if (!saveToSettings) return;
        _appSettings.Device = null;
        _appSettings.Save();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_appSettings.Device == null)
        {
            SelectDevice();
        }
        else
        {
            LoadDevice(_appSettings.Device, false);
        }
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.AudioPlaybackEngine?.OutputDevice?.Dispose();
        _appSettings.Save();
        Application.Current.Shutdown();
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose) return;
        e.Cancel = true;
        Hide();
    }

    private void AsioOut_DriverResetRequest(object? sender, EventArgs e)
    {
        var deviceDescription = _viewModel.DeviceDescription!;
        DisposeDevice(false);
        Dispatcher.Invoke(() => LoadDevice(deviceDescription, false));
    }

    private void miCloseApp_OnClick(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        Close();
    }

    private void btnDisposeDevice_OnClick(object sender, RoutedEventArgs e)
    {
        DisposeDevice(true);
    }

    private void btnChangeDevice_OnClick(object sender, RoutedEventArgs e)
    {
        SelectDevice();
    }

    private void btnChangeKey_OnClick(object sender, RoutedEventArgs e)
    {

    }

    private void btnAsioControlPanel_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.AudioPlaybackEngine?.OutputDevice is AsioOut asioOut)
        {
            asioOut.ShowControlPanel();
        }
    }

    private void RangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel.AudioPlaybackEngine is null) return;
        if (!_appSettings.VolumeEnabled) return;

        _appSettings.Volume = (float)((Slider)sender).Value;
    }
}