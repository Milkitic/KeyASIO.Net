using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Core.Audio.Utils;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Services;
using KeyAsio.Utils;
using KeyAsio.ViewModels;
using KeyAsio.Views.Dialogs;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Toasts;

namespace KeyAsio.Views;

public partial class MainWindow : SukiWindow
{
    private readonly ILogger<MainWindow> _logger;
    private readonly MainWindowViewModel _viewModel;
    private readonly SkinManager _skinManager;

    public MainWindow(ILogger<MainWindow> logger, MainWindowViewModel mainWindowViewModel, SkinManager skinManager)
    {
        _logger = logger;
        _skinManager = skinManager;
        DataContext = _viewModel = mainWindowViewModel;
        InitializeComponent();
        ToastHost.Manager = _viewModel.MainToastManager;
        BindOptions();
    }

    private void BindOptions()
    {
        ConsoleManager.BindExitAction(() =>
        {
            Dispatcher.UIThread.Invoke(Close);
            Thread.Sleep(1000);
        });

        var appSettings = _viewModel.AppSettings;
        appSettings.Logging.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettingsLogging.EnableDebugConsole))
            {
                if (appSettings.Logging.EnableDebugConsole)
                {
                    ConsoleManager.Show();
                }
                else
                {
                    ConsoleManager.Hide();
                }
            }
        };
        appSettings.Performance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettingsPerformance.EnableAvx512))
            {
                SimdAudioConverter.EnableAvx512 = appSettings.Performance.EnableAvx512;
                ProfessionalBalanceProvider.EnableAvx512 = appSettings.Performance.EnableAvx512;
            }
        };
    }

    #region For Designer

    public MainWindow()
    {
        if (!Design.IsDesignMode)
            throw new NotSupportedException();
        InitializeComponent();
    }

    #endregion

    private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Design.IsDesignMode) return;

            _viewModel.SettingsPageItem = SettingsMenuItem;
            _viewModel.AudioEnginePageItem = AudioEngineMenuItem;

            InitializeTrayIcon();

            if (_viewModel.AppSettings.Paths.AllowAutoLoadSkins == null)
            {
                _viewModel.MainToastManager.CreateToast()
                    .WithTitle("Load Skins")
                    .WithContent("Do you want to load skins from osu! folder?")
                    .WithActionButton("No", _ =>
                    {
                        _viewModel.AppSettings.Paths.AllowAutoLoadSkins = false;
                        _viewModel.AppSettings.Save();
                    }, true, SukiButtonStyles.Basic)
                    .WithActionButton("Yes", _ =>
                    {
                        _viewModel.AppSettings.Paths.AllowAutoLoadSkins = true;
                        _viewModel.AppSettings.Save();
                        _skinManager.ReloadSkinsAsync();
                    }, true)
                    .Queue();
            }

            if (_viewModel.AppSettings.Logging.EnableErrorReporting == null)
            {
                _viewModel.MainToastManager.CreateToast()
                    .WithTitle("Enable Error Reporting")
                    .WithContent("Send logs and errors to developer?\r\nYou can change option later.")
                    .WithActionButton("No", _ =>
                    {
                        _viewModel.AppSettings.Logging.EnableErrorReporting = false;
                        _viewModel.AppSettings.Save();
                    }, true, SukiButtonStyles.Basic)
                    .WithActionButton("Yes", _ =>
                    {
                        _viewModel.AppSettings.Logging.EnableErrorReporting = true;
                        _viewModel.AppSettings.Save();
                    }, true)
                    .Queue();
            }

            var updateService = _viewModel.UpdateService;
            updateService.UpdateAction = () => StartUpdate(updateService);
            updateService.CheckUpdateCallback = (res) =>
            {
                if (res == true)
                {
                    ShowUpdateToast(updateService);
                }
                else
                {
                    _viewModel.MainToastManager.CreateSimpleInfoToast()
                        .WithTitle("Check for Updates")
                        .WithContent("You are using the latest version.")
                        .Queue();
                }
            };

            var result = await updateService.CheckUpdateAsync();
            if (result == true)
            {
                ShowUpdateToast(updateService);
            }

            _viewModel.AudioSettings.OnDeviceChanged += AudioSettings_OnDeviceChanged;
            await _viewModel.AudioSettings.InitializeDevice();

            if (_viewModel.AudioSettings.DeviceErrorMessage != null)
            {
                _viewModel.MainToastManager.CreateToast()
                    .WithTitle("Device Initialization Failed")
                    .WithContent(_viewModel.AudioSettings.DeviceErrorMessage)
                    .OfType(NotificationType.Error)
                    .Dismiss().After(TimeSpan.FromSeconds(8))
                    .Dismiss().ByClicking()
                    .Queue();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates or initializing audio.");
        }
    }

    private void ShowUpdateToast(UpdateService updateService)
    {
        _viewModel.MainToastManager.CreateToast()
            .WithTitle("Update Available")
            .WithContent($"Update {updateService.NewVersion} is Now Available.")
            .WithActionButton("Later", _ => { }, true, SukiButtonStyles.Basic)
            .WithActionButton("Update", _ => StartUpdate(updateService), true)
            .Queue();
    }

    private void StartUpdate(UpdateService updateService)
    {
        _viewModel.DialogManager.CreateDialog()
            .WithTitle("Updating")
            .WithContent(new UpdateDialogView { DataContext = updateService })
            .WithActionButton("Cancel", _ => updateService.CancelUpdate(), true)
            .TryShow();

        _ = updateService.DownloadAndInstallAsync();
    }

    private void AudioSettings_OnDeviceChanged(DeviceDescription? device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (device == null)
            {
                BackgroundStyle = SukiBackgroundStyle.Flat;
                SukiTheme.GetInstance().ChangeColorTheme(SukiColor.Blue);
            }
            else
            {
                //BackgroundStyle = SukiBackgroundStyle.GradientSoft;
                if (device.WavePlayerType == WavePlayerType.ASIO)
                {
                    SukiTheme.GetInstance().ChangeColorTheme(SukiColor.Red);
                }
                else if (device is { WavePlayerType: WavePlayerType.WASAPI, IsExclusive: true })
                {
                    SukiTheme.GetInstance().ChangeColorTheme(SukiColor.Orange);
                }
                else
                {
                    SukiTheme.GetInstance().ChangeColorTheme(SukiColor.Blue);
                }
            }
        });
    }

    private NativeMenuItem? _asioOptionsMenuItem;

    private void InitializeTrayIcon()
    {
        var trayIcon = TrayIcon.GetIcons(Avalonia.Application.Current!).FirstOrDefault();
        if (trayIcon != null)
        {
            // Bind click command
            trayIcon.Command = _viewModel.ShowWindowCommand;

            // Create Menu
            var menu = new NativeMenu();
            
            var dashboardItem = new NativeMenuItem("Open Dashboard");
            dashboardItem.Command = _viewModel.ShowWindowCommand;
            menu.Items.Add(dashboardItem);

            _asioOptionsMenuItem = new NativeMenuItem("Open ASIO Options");
            _asioOptionsMenuItem.Command = _viewModel.AudioSettings.OpenAsioPanelCommand;
            // Initially check visibility
            if (_viewModel.AudioSettings.IsAsio)
            {
                menu.Items.Add(_asioOptionsMenuItem);
            }

            menu.Items.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("Exit");
            exitItem.Command = _viewModel.ExitApplicationCommand;
            menu.Items.Add(exitItem);

            trayIcon.Menu = menu;

            UpdateAsioMenuVisibility(trayIcon);
            _viewModel.AudioSettings.PropertyChanged += (s, e) => AudioSettings_PropertyChanged(s, e, trayIcon);
        }
    }

    private void AudioSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e, TrayIcon trayIcon)
    {
        if (e.PropertyName == nameof(AudioSettingsViewModel.IsAsio))
        {
            Dispatcher.UIThread.Post(() => UpdateAsioMenuVisibility(trayIcon));
        }
    }

    private void UpdateAsioMenuVisibility(TrayIcon trayIcon)
    {
        if (trayIcon.Menu?.Items is { } items && _asioOptionsMenuItem != null)
        {
            bool isAsio = _viewModel.AudioSettings.IsAsio;
            if (isAsio && !items.Contains(_asioOptionsMenuItem))
            {
                // Insert at index 1 (after "Open Dashboard")
                if (items.Count >= 1)
                    items.Insert(1, _asioOptionsMenuItem);
                else
                    items.Add(_asioOptionsMenuItem);
            }
            else if (!isAsio && items.Contains(_asioOptionsMenuItem))
            {
                items.Remove(_asioOptionsMenuItem);
            }
        }
    }
}