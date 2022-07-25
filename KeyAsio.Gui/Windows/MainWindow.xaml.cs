using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;
using HandyControl.Data;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.UserControls;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Waves;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using Milki.Extensions.MouseKeyHook;
using NAudio.Wave;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : DialogWindow
{
    private static readonly ILogger Logger = LogUtils.GetLogger("STA Window");

    private bool _forceClose;
    private readonly AppSettings _appSettings;
    private readonly SharedViewModel _viewModel;
    private CachedSound? _cacheSound;
    private readonly IKeyboardHook _keyboardHook;
    private readonly List<Guid> _registerList = new();
    private Timer? _timer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel = SharedViewModel.Instance;
        _appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();

        _keyboardHook = KeyboardHookFactory.CreateGlobal();
        CreateShortcuts();
        BindOptions();
    }

    private void CreateShortcuts()
    {
        var ignoreBeatmapHitsound = _appSettings.RealtimeOptions.IgnoreBeatmapHitsoundBindKey;
        if (ignoreBeatmapHitsound?.Keys != null)
        {
            _keyboardHook.RegisterHotkey(ignoreBeatmapHitsound.ModifierKeys, ignoreBeatmapHitsound.Keys.Value,
                (_, _, _) =>
                {
                    _appSettings.RealtimeOptions.IgnoreBeatmapHitsound =
                        !_appSettings.RealtimeOptions.IgnoreBeatmapHitsound;
                    _appSettings.Save();
                });
        }

        var ignoreSliderTicksAndSlides = _appSettings.RealtimeOptions.IgnoreSliderTicksAndSlidesBindKey;
        if (ignoreSliderTicksAndSlides?.Keys != null)
        {
            _keyboardHook.RegisterHotkey(ignoreSliderTicksAndSlides.ModifierKeys, ignoreSliderTicksAndSlides.Keys.Value,
                (_, _, _) =>
                {
                    _appSettings.RealtimeOptions.IgnoreSliderTicksAndSlides =
                        !_appSettings.RealtimeOptions.IgnoreSliderTicksAndSlides;
                    _appSettings.Save();
                });
        }

        var ignoreStoryboardSamples = _appSettings.RealtimeOptions.IgnoreStoryboardSamplesBindKey;
        if (ignoreStoryboardSamples?.Keys != null)
        {
            _keyboardHook.RegisterHotkey(ignoreStoryboardSamples.ModifierKeys, ignoreStoryboardSamples.Keys.Value,
                (_, _, _) =>
                {
                    _appSettings.RealtimeOptions.IgnoreStoryboardSamples =
                        !_appSettings.RealtimeOptions.IgnoreStoryboardSamples;
                    _appSettings.Save();
                });
        }
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
        var forceBufferSize = window.ViewModel.ForceAsioBufferSize;
        deviceDescription.Latency = latency;
        deviceDescription.IsExclusive = isExclusive;
        deviceDescription.ForceASIOBufferSize = forceBufferSize;
        _appSettings.SampleRate = window.ViewModel.SampleRate;

        await LoadDevice(deviceDescription, true);
    }

    private async Task LoadDevice(DeviceDescription deviceDescription, bool saveToSettings)
    {
        try
        {
            var device = DeviceCreationHelper.CreateDevice(out var actualDescription, deviceDescription);
            _viewModel.AudioEngine = new AudioEngine(device, _appSettings.SampleRate)
            {
                Volume = _appSettings.Volume / 100f,
                MusicVolume = _appSettings.RealtimeOptions.MusicTrackVolume / 100f,
                EffectVolume = _appSettings.RealtimeOptions.EffectTrackVolume / 100f
            };

            if (device is AsioOut asioOut)
            {
                asioOut.DriverResetRequest += AsioOut_DriverResetRequest;
                _viewModel.FramesPerBuffer = asioOut.FramesPerBuffer;
                _timer = new Timer(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _viewModel.PlaybackLatency = asioOut.PlaybackLatency;
                        }
                        catch
                        {
                            // ignored
                        }
                    });
                }, null, 0, 100);
            }

            var waveFormat = _viewModel.AudioEngine.WaveFormat;
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
            Logger.Error(ex, $"Error occurs while creating device.");
            LogUtils.LogToSentry(LogLevel.Error, $"Device Creation Error.", ex, scope =>
            {
                scope.SetTag("device.id", deviceDescription.DeviceId ?? "");
                scope.SetTag("device.name", deviceDescription.FriendlyName ?? "");
                scope.SetTag("device.buffer", deviceDescription.ForceASIOBufferSize.ToString());
                scope.SetTag("device.exclusive", deviceDescription.IsExclusive.ToString());
                scope.SetTag("device.latency", deviceDescription.Latency.ToString());
                scope.SetTag("device.type", deviceDescription.WavePlayerType.ToString());
            });
        }
    }

    private void DisposeDevice(bool saveToSettings)
    {
        if (_viewModel.AudioEngine == null) return;

        if (_viewModel.AudioEngine.OutputDevice is AsioOut asioOut)
        {
            asioOut.DriverResetRequest -= AsioOut_DriverResetRequest;
            _timer?.Dispose();
        }

        for (int i = 0; i < 3; i++)
        {
            try
            {
                _viewModel.AudioEngine.OutputDevice?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while disposing device.", true);
                Thread.Sleep(100);
            }
        }
        _viewModel.AudioEngine = null;
        _viewModel.DeviceDescription = null;
        CachedSoundFactory.ClearCacheSounds();
        CachedSoundFactory.ClearCacheSounds("internal");

        if (!saveToSettings) return;
        _appSettings.Device = null;
        _appSettings.Save();
    }

    private void RegisterKey(HookKeys key)
    {
        KeyboardCallback callback = (_, hookKey, action) =>
        {
            if (action != KeyAction.KeyDown) return;

            Logger.Debug($"{hookKey} {action}");

            if (!_appSettings.RealtimeOptions.RealtimeMode)
            {
                if (_cacheSound != null)
                {
                    _viewModel.AudioEngine?.PlaySound(_cacheSound);
                }
                else
                {
                    Logger.Warn("Hitsound is null. Please check your path.");
                }

                return;
            }

            var playbackInfos = _viewModel.RealtimeModeManager.GetKeyAudio(_appSettings.Keys.IndexOf(hookKey), _appSettings.Keys.Count);
            foreach (var playbackInfo in playbackInfos)
            {
                _viewModel.RealtimeModeManager.PlayAudio(playbackInfo);
            }
        };

        _registerList.Add(_keyboardHook.RegisterKey(key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Control, key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Shift, key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Alt, key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Control | HookModifierKeys.Alt, key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Control | HookModifierKeys.Shift, key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Shift | HookModifierKeys.Alt, key, callback));
        _registerList.Add(_keyboardHook.RegisterHotkey(HookModifierKeys.Control | HookModifierKeys.Shift | HookModifierKeys.Alt, key, callback));
    }

    private void BindOptions()
    {
        ConsoleManager.BindExitAction(() =>
        {
            MainWindow_OnClosed(null, null);
        });

        _appSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Debugging))
            {
                if (_appSettings.Debugging)
                {
                    ConsoleManager.Show();
                }
                else
                {
                    ConsoleManager.Hide();
                }

                _appSettings.Save();
            }
        };
        _appSettings.RealtimeOptions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.RealtimeOptions.RealtimeMode))
            {
                NotifyRestart(e.PropertyName);
                _appSettings.Save();
            }
        };
    }

    private void NotifyRestart(string propertyName)
    {
        Growl.Info(new GrowlInfo()
        {
            Message = $"Restart to fully apply option: {propertyName}",
            ShowDateTime = false,
            WaitTime = 1
        });
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Title += $" {Updater.GetVersion()}";

        if (_appSettings.Device == null)
        {
            await Task.Delay(100);
            await SelectDevice();
        }
        else
        {
            await LoadDevice(_appSettings.Device, false);
        }

        foreach (var key in _appSettings.Keys)
        {
            RegisterKey(key);
        }

        if (!_appSettings.SendLogsToDeveloperConfirmed)
        {
            Growl.Ask($"Send logs and errors to developer?\r\n" +
                      $"You can change option later in configuration file.",
            dialogResult =>
            {
                _appSettings.SendLogsToDeveloper = dialogResult;
                _appSettings.SendLogsToDeveloperConfirmed = true;
                return true;
            });
        }

        var result = await Updater.CheckUpdateAsync();
        if (result == true)
        {
            Growl.Ask($"Found new version: {Updater.NewRelease!.NewVerString}. " +
                   $"Click yes to open the release page.",
                dialogResult =>
                {
                    if (dialogResult)
                    {
                        Updater.OpenLastReleasePage();
                    }

                    return true;
                });
        }
    }

    private void MainWindow_OnClosed(object? sender, EventArgs? e)
    {
        DisposeDevice(false);
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

        await Dispatcher.InvokeAsync(async () =>
        {
            DisposeDevice(false);
            await LoadDevice(deviceDescription, false);
        });
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
            _appSettings.Keys = window.ViewModel.Keys.ToList();
            _appSettings.Save();
        }

        foreach (var key in _appSettings.Keys)
        {
            RegisterKey(key);
        }
    }

    private void btnAsioControlPanel_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.AudioEngine?.OutputDevice is AsioOut asioOut)
        {
            asioOut.ShowControlPanel();
        }
    }

    private void RangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel.AudioEngine is null) return;

        var slider = (Slider)sender;
        _appSettings.Volume = (int)Math.Round(slider.Value * 100);
    }

    private void MusicRangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel.AudioEngine is null) return;

        var slider = (Slider)sender;
        _appSettings.RealtimeOptions.MusicTrackVolume = (int)Math.Round(slider.Value * 100);
    }

    private void btnLatencyCheck_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var guid in _registerList)
        {
            _keyboardHook.TryUnregister(guid);
        }

        _registerList.Clear();

        var latencyGuideWindow = new LatencyGuideWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        latencyGuideWindow.ShowDialog();

        foreach (var key in _appSettings.Keys)
        {
            RegisterKey(key);
        }
    }

    private void btnRealtimeOptions_OnClick(object sender, RoutedEventArgs e)
    {
        var latencyGuideWindow = new RealtimeOptionsWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        latencyGuideWindow.ShowDialog();
    }
}