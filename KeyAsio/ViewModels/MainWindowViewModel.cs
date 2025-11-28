using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Logging;
using SukiUI.Dialogs;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Milki.Extensions.Configuration;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel
{
    private readonly ILogger<MainWindowViewModel> _logger;

    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    [ObservableProperty]
    private object? _selectedMenuItem;

    private bool _isNavigating;
    private CancellationTokenSource? _saveDebounceCts;

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
            .WithActionButton("Save", _ =>
            {
                AudioSettings.ApplyAudioSettings();
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
        }
    }

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger,
        AppSettings appSettings,
        UpdateService updateService,
        AudioSettingsViewModel audioSettingsViewModel)
    {
        AppSettings = appSettings;
        UpdateService = updateService;
        _logger = logger;
        AudioSettings = audioSettingsViewModel;

        SubscribeToSettingsChanges();
    }

    public AppSettings AppSettings { get; }
    public UpdateService UpdateService { get; }
    public AudioSettingsViewModel AudioSettings { get; }

    public SliderTailPlaybackBehavior[] SliderTailBehaviors { get; } = Enum.GetValues<SliderTailPlaybackBehavior>();

    private void SubscribeToSettingsChanges()
    {
        void Subscribe(INotifyPropertyChanged? obj)
        {
            if (obj != null)
            {
                obj.PropertyChanged += OnSettingsChanged;
            }
        }

        Subscribe(AppSettings.General);
        Subscribe(AppSettings.Input);
        Subscribe(AppSettings.Paths);
        Subscribe(AppSettings.Audio);
        Subscribe(AppSettings.Logging);
        Subscribe(AppSettings.Performance);
        Subscribe(AppSettings.Realtime);
        Subscribe(AppSettings.Realtime.Scanning);
        Subscribe(AppSettings.Realtime.Playback);
        Subscribe(AppSettings.Realtime.Filters);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsAudio.MasterVolume) ||
            e.PropertyName == nameof(AppSettingsAudio.MusicVolume) ||
            e.PropertyName == nameof(AppSettingsAudio.EffectVolume) ||
            e.PropertyName == nameof(AppSettingsRealtimePlayback.BalanceFactor))
        {
            DebounceSave();
        }
        else
        {
            AppSettings.Save();
        }
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
                    _logger?.LogError(ex, "Failed to save settings");
                }
            });
        });
    }
}