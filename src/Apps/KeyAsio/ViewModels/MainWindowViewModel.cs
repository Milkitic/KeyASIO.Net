using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Secrets;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync;
using Microsoft.Extensions.Logging;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel : IDisposable
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly SettingsManager _settingsManager;
    private bool _isNavigating;
    private bool _disposed;

    public MainWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new NotSupportedException();
        }

        DialogManager = new SukiDialogManager();
        MainToastManager = new SukiToastManager();
        AppSettings = new AppSettings();
        AudioSettings = new AudioSettingsViewModel();
        Shared = new SharedViewModel(AppSettings);
        SyncSession = new SyncSessionContext(AppSettings);
        SyncDisplay = new SyncDisplayViewModel(SyncSession);

        PluginManager = new PluginManagerViewModel(null!, AppSettings);
        KeyBinding = new KeyBindingViewModel(null!, DialogManager, AppSettings, null!);

        LanguageManager = new LanguageManager(null!, AppSettings);

        UpdateService = null!;
        _logger = null!;
        IsVerified = VerifyUtils.IsOfficialBuildUnsafe();
#if DEBUG
        IsDevelopment = true;
#endif
    }

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger,
        AppSettings appSettings,
        UpdateService updateService,
        AudioSettingsViewModel audioSettingsViewModel,
        SharedViewModel sharedViewModel,
        SyncSessionContext syncSession,
        PluginManagerViewModel pluginManagerViewModel,
        KeyBindingViewModel keyBindingViewModel,
        ISukiDialogManager dialogManager,
        ISukiToastManager toastManager,
        LanguageManager languageManager)
    {
        AppSettings = appSettings;
        UpdateService = updateService;
        _logger = logger;
        AudioSettings = audioSettingsViewModel;
        Shared = sharedViewModel;
        SyncSession = syncSession;
        SyncDisplay = new SyncDisplayViewModel(SyncSession);

        DialogManager = dialogManager;
        MainToastManager = toastManager;
        AudioSettings.ToastManager = MainToastManager;

        PluginManager = pluginManagerViewModel;
        KeyBinding = keyBindingViewModel;

        LanguageManager = languageManager;
        IsVerified = VerifyUtils.IsOfficialBuildUnsafe();
#if DEBUG
        IsDevelopment = true;
#endif
    }

    public LanguageManager LanguageManager { get; }
    public ISukiDialogManager DialogManager { get; }
    public ISukiToastManager MainToastManager { get; }
    public AppSettings AppSettings { get; }
    public UpdateService UpdateService { get; }
    public AudioSettingsViewModel AudioSettings { get; }
    public SharedViewModel Shared { get; }
    public SyncSessionContext SyncSession { get; }
    public SyncDisplayViewModel SyncDisplay { get; }
    public KeyBindingViewModel KeyBinding { get; }
    public PluginManagerViewModel PluginManager { get; }
    public bool IsDevelopment { get; }

    public SliderTailPlaybackBehavior[] SliderTailBehaviors { get; } = Enum.GetValues<SliderTailPlaybackBehavior>();
    public AppTheme[] AvailableThemes { get; } = Enum.GetValues<AppTheme>();

    [ObservableProperty]
    public partial object? SelectedMenuItem { get; set; }

    [ObservableProperty]
    public partial bool IsVerified { get; set; }

    public object? SettingsPageItem { get; set; }
    public object? AudioEnginePageItem { get; set; }

    [ObservableProperty]
    public partial bool IsExiting { get; set; }
    
    [ObservableProperty]
    public partial int Hue { get; set; } = 150;

    [RelayCommand]
    public void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Show();
            desktop.MainWindow?.Activate();
            desktop.MainWindow?.Focus();
        }
    }

    [RelayCommand]
    public void ExitApplication()
    {
        IsExiting = true;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    public void NavigateToSettings()
    {
        if (SettingsPageItem != null)
        {
            NavigateTo(SettingsPageItem);
        }
    }

    [RelayCommand]
    public void NavigateToAudioEngine()
    {
        if (AudioEnginePageItem != null)
        {
            NavigateTo(AudioEnginePageItem);
        }
    }

    [RelayCommand]
    public void ShowDeviceError()
    {
        if (AudioSettings.DeviceErrorMessage == null) return;

        DialogManager.CreateDialog()
            .WithTitle("Device Fault")
            .WithContent(new ScrollViewer
            {
                Content = new SelectableTextBlock
                {
                    Text = AudioSettings.DeviceFullErrorMessage ?? AudioSettings.DeviceErrorMessage,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas")
                },
                MaxHeight = 300
            })
            .WithActionButton("OK", _ => { }, true)
            .TryShow();
    }

    [RelayCommand]
    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open URL: {Url}", url);
        }
    }

    partial void OnSelectedMenuItemChanging(object? oldValue, object? newValue)
    {
        if (_isNavigating) return;

        // If we have unsaved changes and we are navigating away (newValue is different)
        // We assume we are navigating away from AudioEnginePage because that's the only place modifying these settings.
        if (!AudioSettings.HasUnsavedAudioChanges || newValue == null || oldValue == newValue) return;
        _isNavigating = true;
        SelectedMenuItem = oldValue;
        _isNavigating = false;

        DialogManager.CreateDialog()
            .WithTitle("Unsaved Changes")
            .WithContent("You have unsaved changes in Audio Engine settings.\nDo you want to save them before leaving?")
            .OfType(NotificationType.Warning)
            .WithActionButton("Save", async _ =>
            {
                await AudioSettings.ApplyAudioSettings();
                NavigateTo(newValue);
            }, true, classes: "")
            .WithActionButton("Don't Save", _ =>
            {
                AudioSettings.DiscardAudioSettings();
                NavigateTo(newValue);
            }, true, classes: "")
            .WithActionButton("Cancel", _ => { NavigateTo(oldValue); }, true)
            .TryShow();
    }

    private void NavigateTo(object? page)
    {
        _isNavigating = true;
        SelectedMenuItem = page;
        _isNavigating = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SyncDisplay.Dispose();
        PluginManager.Dispose();

        GC.SuppressFinalize(this);
    }
}