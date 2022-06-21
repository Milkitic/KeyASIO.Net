using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using Milki.Extensions.MouseKeyHook;
using NAudio.Wave;
using Window = System.Windows.Window;

namespace KeyAsio.Gui.Windows;

public class MainWindowViewModel : ViewModelBase
{
    private AudioPlaybackEngine? _audioPlaybackEngine;
    private DeviceDescription? _deviceDescription;
    private AppSettings? _appSettings;
    private int _framesPerBuffer;

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

    public int FramesPerBuffer
    {
        get => _framesPerBuffer;
        set => this.RaiseAndSetIfChanged(ref _framesPerBuffer, value);
    }

    public App App { get; } = (App)Application.Current;
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly ILogger Logger = SharedUtils.GetLogger("STA Window");

    private bool _forceClose;
    private readonly AppSettings _appSettings;
    private readonly MainWindowViewModel _viewModel;
    private CachedSound? _cacheSound;
    private readonly IKeyboardHook _keyboardHook;
    private List<Guid> _registerList = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel = new MainWindowViewModel();
        _viewModel.AppSettings = _appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();

        _keyboardHook = KeyboardHookFactory.CreateGlobal();
    }

    private async Task SelectDevice()
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

        await LoadDevice(deviceDescription, true);
    }

    private async Task LoadDevice(DeviceDescription deviceDescription, bool saveToSettings)
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
                _viewModel.FramesPerBuffer = asioOut.FramesPerBuffer;
            }

            var waveFormat = _viewModel.AudioPlaybackEngine.WaveFormat;
            _cacheSound = await CachedSoundFactory.GetOrCreateCacheSound(waveFormat, _appSettings.HitsoundPath);

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
        CachedSoundFactory.ClearCacheSounds();

        if (!saveToSettings) return;
        _appSettings.Device = null;
        _appSettings.Save();
    }

    private void RegisterHotKey(HookKeys key)
    {
        _registerList.Add(_keyboardHook.RegisterKey(key, (_, hookKey, action) =>
        {
            if (action == KeyAction.KeyDown)
            {
                if (_cacheSound != null)
                {
                    _viewModel.AudioPlaybackEngine?.PlaySound(_cacheSound);
                }

                if (_appSettings.Debugging)
                {
                    Logger.LogDebug($"{hookKey} {action}");
                }
            }
            else
            {
                if (_appSettings.Debugging)
                {
                    Logger.LogDebug($"{hookKey} {action}");
                }
            }
        }));
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_appSettings.Device == null)
        {
            await SelectDevice();
        }
        else
        {
            await LoadDevice(_appSettings.Device, false);
        }

        foreach (var key in _appSettings.Keys)
        {
            RegisterHotKey(key);
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

    private async void AsioOut_DriverResetRequest(object? sender, EventArgs e)
    {
        var deviceDescription = _viewModel.DeviceDescription!;
        DisposeDevice(false);
        await Dispatcher.InvokeAsync(async () => await LoadDevice(deviceDescription, false));
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

    private async void btnChangeDevice_OnClick(object sender, RoutedEventArgs e)
    {
        await SelectDevice();
    }

    private void btnChangeKey_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var guid in _registerList)
        {
            _keyboardHook.TryUnregister(guid);
        }

        _registerList.Clear();

        var window = new KeyBindWindow(_appSettings.Keys)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (window.ShowDialog() == true)
        {
            _appSettings.Keys = window.ViewModel.Keys.ToHashSet();
        }

        foreach (var key in _appSettings.Keys)
        {
            RegisterHotKey(key);
        }
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