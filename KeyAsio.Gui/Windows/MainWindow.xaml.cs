using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;
using HandyControl.Data;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Gui.UserControls;
using KeyAsio.Gui.Utils;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly AudioCacheManager _audioCacheManager;
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly SharedViewModel _viewModel;
    private CachedAudio? _cacheSound;
    private readonly IKeyboardHook _keyboardHook;
    private readonly List<Guid> _registerList = new();
    private Timer? _timer;

    public MainWindow(AppSettings appSettings,
        AudioEngine audioEngine,
        AudioCacheManager audioCacheManager,
        RealtimeModeManager realtimeModeManager,
        AudioDeviceManager audioDeviceManager,
        SharedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        AppSettings = appSettings;
        AudioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
        RealtimeModeManager = realtimeModeManager;
        _audioDeviceManager = audioDeviceManager;

        _keyboardHook = KeyboardHookFactory.CreateGlobal();
        CreateShortcuts();
        BindOptions();
    }

    public AppSettings AppSettings { get; }
    public RealtimeModeManager RealtimeModeManager { get; }
    public AudioEngine AudioEngine { get; }

    private void CreateShortcuts()
    {
        var ignoreBeatmapHitsound = AppSettings.RealtimeOptions.IgnoreBeatmapHitsoundBindKey;
        if (ignoreBeatmapHitsound?.Keys != null)
        {
            _keyboardHook.RegisterHotkey(ignoreBeatmapHitsound.ModifierKeys, ignoreBeatmapHitsound.Keys.Value,
                (_, _, _) =>
                {
                    AppSettings.RealtimeOptions.IgnoreBeatmapHitsound =
                        !AppSettings.RealtimeOptions.IgnoreBeatmapHitsound;
                    AppSettings.Save();
                });
        }

        var ignoreSliderTicksAndSlides = AppSettings.RealtimeOptions.IgnoreSliderTicksAndSlidesBindKey;
        if (ignoreSliderTicksAndSlides?.Keys != null)
        {
            _keyboardHook.RegisterHotkey(ignoreSliderTicksAndSlides.ModifierKeys, ignoreSliderTicksAndSlides.Keys.Value,
                (_, _, _) =>
                {
                    AppSettings.RealtimeOptions.IgnoreSliderTicksAndSlides =
                        !AppSettings.RealtimeOptions.IgnoreSliderTicksAndSlides;
                    AppSettings.Save();
                });
        }

        var ignoreStoryboardSamples = AppSettings.RealtimeOptions.IgnoreStoryboardSamplesBindKey;
        if (ignoreStoryboardSamples?.Keys != null)
        {
            _keyboardHook.RegisterHotkey(ignoreStoryboardSamples.ModifierKeys, ignoreStoryboardSamples.Keys.Value,
                (_, _, _) =>
                {
                    AppSettings.RealtimeOptions.IgnoreStoryboardSamples =
                        !AppSettings.RealtimeOptions.IgnoreStoryboardSamples;
                    AppSettings.Save();
                });
        }
    }

    private async Task SelectDevice()
    {
        var window = ((App)Application.Current).Services.GetRequiredService<DeviceWindow>();
        window.Owner = this;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (window.ShowDialog() != true) return;

        var deviceDescription = window.ViewModel.SelectedDevice;
        if (deviceDescription == null) return;

        DisposeDevice(false);

        var latency = window.ViewModel.Latency;
        var isExclusive = window.ViewModel.IsExclusive;
        var forceBufferSize = window.ViewModel.ForceAsioBufferSize;
        var configuredDeviceDescription = deviceDescription with
        {
            Latency = latency,
            IsExclusive = isExclusive,
            ForceASIOBufferSize = forceBufferSize
        };

        AppSettings.SampleRate = window.ViewModel.SampleRate;

        await LoadDevice(configuredDeviceDescription, true);
    }

    private async Task LoadDevice(DeviceDescription deviceDescription, bool saveToSettings)
    {
        try
        {
            var (device, actualDescription) = _audioDeviceManager.CreateDevice(deviceDescription);
            AudioEngine.MainVolume = AppSettings.Volume / 100f;
            AudioEngine.MusicVolume = AppSettings.RealtimeOptions.MusicTrackVolume / 100f;
            AudioEngine.EffectVolume = AppSettings.RealtimeOptions.EffectTrackVolume / 100f;
            AudioEngine.StartDevice(device);

            if (device is AsioOut asioOut)
            {
                asioOut.DriverResetRequest += AsioOut_DriverResetRequest;
                _viewModel.FramesPerBuffer = asioOut.FramesPerBuffer;
                _timer = new Timer(_ =>
                {
                    try
                    {
                        Dispatcher?.Invoke(() =>
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
                    }
                    catch
                    {
                        // ignored
                    }
                }, null, 0, 100);
            }

            var waveFormat = AudioEngine.EngineWaveFormat;
            if (Path.Exists(AppSettings.HitsoundPath))
            {
                await using var fs = File.OpenRead(AppSettings.HitsoundPath);
                var (cachedAudio, result) = await _audioCacheManager.GetOrCreateOrEmptyAsync(AppSettings.HitsoundPath, fs, waveFormat);
                _cacheSound = cachedAudio;
            }

            _viewModel.DeviceDescription = actualDescription;
            if (saveToSettings)
            {
                AppSettings.Device = actualDescription;
                AppSettings.Save();
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
        if (AudioEngine.CurrentDevice is AsioOut asioOut)
        {
            asioOut.DriverResetRequest -= AsioOut_DriverResetRequest;
            _timer?.Dispose();
        }

        for (int i = 0; i < 3; i++)
        {
            try
            {
                AudioEngine.CurrentDevice?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while disposing device.", true);
                Thread.Sleep(100);
            }
        }

        AudioEngine.StopDevice();
        _viewModel.DeviceDescription = null;
        _audioCacheManager.Clear();
        _audioCacheManager.Clear("internal");

        if (!saveToSettings) return;
        AppSettings.Device = null;
        AppSettings.Save();
    }

    private void RegisterKey(HookKeys key)
    {
        KeyboardCallback callback = (_, hookKey, action) =>
        {
            if (action != KeyAction.KeyDown) return;

            Logger.Debug($"{hookKey} {action}");

            if (!AppSettings.RealtimeOptions.RealtimeMode)
            {
                if (_cacheSound != null)
                {
                    if (AudioEngine != null)
                    {
                        AudioEngine.PlayAudio(_cacheSound);
                    }
                    else
                    {
                        Logger.Warn("AudioEngine not ready.");
                    }
                }
                else
                {
                    Logger.Warn("Hitsound is null. Please check your path.");
                }

                return;
            }

            var playbackInfos =
                RealtimeModeManager.GetKeyAudio(AppSettings.Keys.IndexOf(hookKey), AppSettings.Keys.Count);
            foreach (var playbackInfo in playbackInfos)
            {
                RealtimeModeManager.PlayAudio(playbackInfo);
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
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("User manually closes debug window. Program will now exit.");
            Console.ResetColor();
            Dispatcher.Invoke(ForceClose);
            Thread.Sleep(1000);
        });
        AppSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Debugging))
            {
                if (AppSettings.Debugging)
                {
                    ConsoleManager.Show();
                }
                else
                {
                    ConsoleManager.Hide();
                }

                AppSettings.Save();
            }
        };
        AppSettings.RealtimeOptions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.RealtimeOptions.RealtimeMode))
            {
                NotifyRestart(e.PropertyName);
                AppSettings.Save();
            }
            else if (e.PropertyName == nameof(AppSettings.RealtimeOptions.EnableMusicFunctions))
            {
                AppSettings.Save();
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

    private void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        var version = Updater.GetVersion();
        FixCommit(ref version);
        Title += $" {version}";

        if (AppSettings.Device == null)
        {
            await Task.Delay(100);
            await SelectDevice();
        }
        else
        {
            await LoadDevice(AppSettings.Device, false);
        }

        foreach (var key in AppSettings.Keys)
        {
            RegisterKey(key);
        }

        if (!AppSettings.SendLogsToDeveloperConfirmed)
        {
            Growl.Ask($"Send logs and errors to developer?\r\n" +
                      $"You can change option later in configuration file.",
                dialogResult =>
                {
                    AppSettings.SendLogsToDeveloper = dialogResult;
                    AppSettings.SendLogsToDeveloperConfirmed = true;
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
        AppSettings.Save();
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
        ForceClose();
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

        var window = new KeyBindWindow(AppSettings.Keys)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (window.ShowDialog() == true)
        {
            AppSettings.Keys = window.ViewModel.Keys.ToList();
            AppSettings.Save();
        }

        foreach (var key in AppSettings.Keys)
        {
            RegisterKey(key);
        }
    }

    private void btnAsioControlPanel_OnClick(object sender, RoutedEventArgs e)
    {
        if (AudioEngine.CurrentDevice is AsioOut asioOut)
        {
            asioOut.ShowControlPanel();
        }
    }

    private void RangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var slider = (Slider)sender;
        AppSettings.Volume = (int)Math.Round(slider.Value * 100);
    }

    private void MusicRangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var slider = (Slider)sender;
        AppSettings.RealtimeOptions.MusicTrackVolume = (int)Math.Round(slider.Value * 100);
    }

    private void btnLatencyCheck_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var guid in _registerList)
        {
            _keyboardHook.TryUnregister(guid);
        }

        _registerList.Clear();
        var latencyGuideWindow = ((App)Application.Current).Services.GetRequiredService<LatencyGuideWindow>();
        latencyGuideWindow.Owner = this;
        latencyGuideWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        latencyGuideWindow.ShowDialog();

        foreach (var key in AppSettings.Keys)
        {
            RegisterKey(key);
        }
    }

    private void btnRealtimeOptions_OnClick(object sender, RoutedEventArgs e)
    {
        var optionsWindow = ((App)Application.Current).Services.GetRequiredService<RealtimeOptionsWindow>();
        optionsWindow.Owner = this;
        optionsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        optionsWindow.ShowDialog();
    }

    private static void FixCommit(ref string version)
    {
        var lastIndexOf = version.LastIndexOf('+');
        if (lastIndexOf >= 0)
        {
            if (version.Length > lastIndexOf + 8)
            {
                version = version.Substring(0, lastIndexOf + 8);
            }
        }
    }
}