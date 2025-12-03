using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Utils;
using KeyAsio.ViewModels;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Toasts;

namespace KeyAsio.Views;

public partial class MainWindow : SukiWindow
{
    private readonly ILogger<MainWindow> _logger;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(ILogger<MainWindow> logger, MainWindowViewModel mainWindowViewModel)
    {
        _logger = logger;
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

                appSettings.Save();
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

            if (!_viewModel.AppSettings.Logging.ErrorReportingConfirmed)
            {
                _viewModel.MainToastManager.CreateToast()
                    .WithTitle("Enable Error Reporting")
                    .WithContent("Send logs and errors to developer?\r\nYou can change option later.")
                    .WithActionButton("No", _ =>
                    {
                        _viewModel.AppSettings.Logging.EnableErrorReporting = false;
                        _viewModel.AppSettings.Logging.ErrorReportingConfirmed = true;
                        _viewModel.AppSettings.Save();
                    }, true, SukiButtonStyles.Basic)
                    .WithActionButton("Yes", _ =>
                    {
                        _viewModel.AppSettings.Logging.EnableErrorReporting = true;
                        _viewModel.AppSettings.Logging.ErrorReportingConfirmed = true;
                        _viewModel.AppSettings.Save();
                    }, true)
                    .Queue();
            }

            //var theme = SukiTheme.GetInstance();
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates.");
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
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100 };
        var statusText = new TextBlock { Text = "Starting..." };
        var stackPanel = new StackPanel
        {
            Spacing = 10,
            Children = { statusText, progressBar }
        };

        var progressBinding = new Binding(nameof(UpdateService.DownloadProgress)) { Source = updateService };
        progressBar.Bind(RangeBase.ValueProperty, progressBinding);

        var statusBinding = new Binding(nameof(UpdateService.StatusMessage)) { Source = updateService };
        statusText.Bind(TextBlock.TextProperty, statusBinding);

        _viewModel.DialogManager.CreateDialog()
            .WithTitle("Updating")
            .WithContent(stackPanel)
            .WithActionButton("Cancel", _ => updateService.CancelUpdate(), true)
            .TryShow();

        _ = updateService.DownloadAndInstallAsync();
    }
}