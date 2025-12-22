using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel : IDisposable
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly KeyboardBindingInitializer _keyboardBindingInitializer;
    private bool _isNavigating;
    private CancellationTokenSource? _saveDebounceCts;
    private readonly List<INotifyPropertyChanged> _observedSettings = new();
    private bool _disposed;

    public MainWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new NotSupportedException();
        }
        else
        {
            AppSettings = new AppSettings();
            AudioSettings = new AudioSettingsViewModel();
            Shared = new SharedViewModel(AppSettings);
            SyncSession = new SyncSessionContext(AppSettings);
            SyncDisplay = new SyncDisplayViewModel(SyncSession);
            _keyboardBindingInitializer = null!;
        }
    }

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger,
        AppSettings appSettings,
        UpdateService updateService,
        AudioSettingsViewModel audioSettingsViewModel,
        SharedViewModel sharedViewModel,
        SyncSessionContext syncSession,
        KeyboardBindingInitializer keyboardBindingInitializer)
    {
        AppSettings = appSettings;
        UpdateService = updateService;
        _logger = logger;
        AudioSettings = audioSettingsViewModel;
        Shared = sharedViewModel;
        SyncSession = syncSession;
        SyncDisplay = new SyncDisplayViewModel(SyncSession);
        _keyboardBindingInitializer = keyboardBindingInitializer;
        AudioSettings.ToastManager = MainToastManager;

        SubscribeToSettingsChanges();
    }

    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();
    public ISukiToastManager MainToastManager { get; } = new SukiToastManager();
    public AppSettings AppSettings { get; }
    public UpdateService UpdateService { get; }
    public AudioSettingsViewModel AudioSettings { get; }
    public SharedViewModel Shared { get; }
    public SyncSessionContext SyncSession { get; }
    public SyncDisplayViewModel SyncDisplay { get; }
    public SliderTailPlaybackBehavior[] SliderTailBehaviors { get; } = Enum.GetValues<SliderTailPlaybackBehavior>();

    [ObservableProperty]
    public partial object? SelectedMenuItem { get; set; }

    public object? SettingsPageItem { get; set; }
    public object? AudioEnginePageItem { get; set; }

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

    private void SubscribeToSettingsChanges()
    {
        void Subscribe(INotifyPropertyChanged? obj)
        {
            if (obj != null)
            {
                obj.PropertyChanged += OnSettingsChanged;
                _observedSettings.Add(obj);
            }
        }

        Subscribe(AppSettings.General);
        Subscribe(AppSettings.Input);
        Subscribe(AppSettings.Paths);
        Subscribe(AppSettings.Audio);
        Subscribe(AppSettings.Logging);
        Subscribe(AppSettings.Performance);
        Subscribe(AppSettings.Sync);
        Subscribe(AppSettings.Sync.Scanning);
        Subscribe(AppSettings.Sync.Playback);
        Subscribe(AppSettings.Sync.Filters);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SyncDisplay.Dispose();

        foreach (var obj in _observedSettings)
        {
            obj.PropertyChanged -= OnSettingsChanged;
        }

        _observedSettings.Clear();
        _saveDebounceCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsAudio.MasterVolume))
        {
            AudioSettings.AudioEngine.MainVolume = AppSettings.Audio.MasterVolume / 100f;
            DebounceSave();
        }
        else if (e.PropertyName == nameof(AppSettingsAudio.MusicVolume))
        {
            AudioSettings.AudioEngine.MusicVolume = AppSettings.Audio.MusicVolume / 100f;
            DebounceSave();
        }
        else if (e.PropertyName == nameof(AppSettingsAudio.EffectVolume))
        {
            AudioSettings.AudioEngine.EffectVolume = AppSettings.Audio.EffectVolume / 100f;
            DebounceSave();
        }
        else if (e.PropertyName == nameof(AppSettingsSyncPlayback.BalanceFactor))
        {
            DebounceSave();
        }
        else
        {
            try
            {
                AppSettings.Save();
            }
            catch (Exception ex)
            {
                HandleSaveException(ex);
            }
        }
    }

    private void HandleSaveException(Exception ex)
    {
        _logger?.LogError(ex, "Failed to save settings");
        Dispatcher.UIThread.Post(() =>
        {
            MainToastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Settings Save Failed")
                .WithContent($"Could not save configuration: {ex.Message}")
                .Queue();
        });
    }

    private void DebounceSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;

        Task.Delay(500, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    AppSettings.Save();
                }
                catch (Exception ex)
                {
                    HandleSaveException(ex);
                }
            });
        });
    }
}