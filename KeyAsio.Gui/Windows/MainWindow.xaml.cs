using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;
using HandyControl.Data;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Gui.Services;
using KeyAsio.Gui.UserControls;
using KeyAsio.Gui.Utils;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using LogLevel = KeyAsio.MemoryReading.Logging.LogLevel;

namespace KeyAsio.Gui.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : DialogWindow
{
    private bool _forceClose;
    private readonly ILogger<MainWindow> _logger;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly SharedViewModel _viewModel;
    private readonly KeyboardBindingInitializer _bindingInitializer;
    private Timer? _timer;

    public MainWindow(
        ILogger<MainWindow> logger,
        AppSettings appSettings,
        AudioEngine audioEngine,
        AudioCacheManager audioCacheManager,
        RealtimeController realtimeController,
        AudioDeviceManager audioDeviceManager,
        SharedViewModel viewModel,
        KeyboardBindingInitializer keyboardBindingInitializer)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        AppSettings = appSettings;
        AudioEngine = audioEngine;
        _logger = logger;
        _audioCacheManager = audioCacheManager;
        RealtimeController = realtimeController;
        _audioDeviceManager = audioDeviceManager;

        _bindingInitializer = keyboardBindingInitializer;
        _bindingInitializer.Setup();
        BindOptions();
    }

    public AppSettings AppSettings { get; }
    public RealtimeController RealtimeController { get; }
    public AudioEngine AudioEngine { get; }

    private async Task SelectDevice()
    {
        var window = ((App)Application.Current).Services.GetRequiredService<DeviceWindow>();
        window.Owner = this;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (window.ShowDialog() != true) return;

        var deviceDescription = window.ViewModel.SelectedDevice;
        if (deviceDescription == null) return;

        await DisposeDeviceAsync(false);

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
            AudioEngine.EnableLimiter = AppSettings.EnableLimiter;
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

            await _bindingInitializer.InitializeKeyAudioAsync();

            _viewModel.DeviceDescription = actualDescription;
            if (saveToSettings)
            {
                AppSettings.Device = actualDescription;
                AppSettings.Save();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurs while creating device.");
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

    private async ValueTask DisposeDeviceAsync(bool saveToSettings)
    {
        if (AudioEngine.CurrentDevice is AsioOut asioOut)
        {
            asioOut.DriverResetRequest -= AsioOut_DriverResetRequest;
            if (_timer != null) await _timer.DisposeAsync();
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
                _logger.LogError(ex, "Error while disposing device.");
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

        _bindingInitializer.RegisterKeys(AppSettings.Keys);

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

    private async void MainWindow_OnClosed(object? sender, EventArgs? e)
    {
        await DisposeDeviceAsync(false);
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
            await DisposeDeviceAsync(false);
            await LoadDevice(deviceDescription, false);
        });
    }

    private void miCloseApp_OnClick(object sender, RoutedEventArgs e)
    {
        ForceClose();
    }

    private async void btnDisposeDevice_OnClick(object sender, RoutedEventArgs e)
    {
        await DisposeDeviceAsync(true);
    }

    private async void btnChangeDevice_OnClick(object sender, RoutedEventArgs e)
    {
        await SelectDevice();
    }

    private void btnChangeKey_OnClick(object sender, RoutedEventArgs e)
    {
        _bindingInitializer.UnregisterAll();

        var window = new KeyBindWindow(AppSettings)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (window.ShowDialog() == true)
        {
            AppSettings.Keys = window.ViewModel.Keys.ToList();
            AppSettings.Save();
        }

        _bindingInitializer.RegisterKeys(AppSettings.Keys);
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
        _bindingInitializer.UnregisterAll();
        var latencyGuideWindow = ((App)Application.Current).Services.GetRequiredService<LatencyGuideWindow>();
        latencyGuideWindow.Owner = this;
        latencyGuideWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        latencyGuideWindow.ShowDialog();
        _bindingInitializer.RegisterKeys(AppSettings.Keys);
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