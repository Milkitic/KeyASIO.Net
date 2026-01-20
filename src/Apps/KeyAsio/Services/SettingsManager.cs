using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using Avalonia.Threading;
using KeyAsio.Core.Audio;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using SukiUI.Toasts;

namespace KeyAsio.Services;

public class SettingsManager : IDisposable
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioEngine _audioEngine;
    private readonly ISukiToastManager _toastManager;

    private readonly List<INotifyPropertyChanged> _observedSettings = new();
    private CancellationTokenSource? _saveDebounceCts;
    private bool _disposed;

    public SettingsManager(
        ILogger<SettingsManager> logger,
        AppSettings appSettings,
        AudioEngine audioEngine,
        ISukiToastManager toastManager)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioEngine = audioEngine;
        _toastManager = toastManager;

        ApplyTheme();
        SubscribeToSettingsChanges();
    }

    private void ApplyTheme()
    {
        if (Application.Current == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            var theme = _appSettings.General.Theme;
            Application.Current.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        });
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

        Subscribe(_appSettings.General);
        Subscribe(_appSettings.Input);
        Subscribe(_appSettings.Paths);
        Subscribe(_appSettings.Audio);
        Subscribe(_appSettings.Logging);
        Subscribe(_appSettings.Performance);
        Subscribe(_appSettings.Sync);
        Subscribe(_appSettings.Sync.Scanning);
        Subscribe(_appSettings.Sync.Playback);
        Subscribe(_appSettings.Sync.Filters);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AppSettingsAudio)
        {
            if (e.PropertyName == nameof(AppSettingsAudio.MasterVolume))
            {
                _audioEngine.MainVolume = _appSettings.Audio.MasterVolume / 100f;
                DebounceSave();
            }
            else if (e.PropertyName == nameof(AppSettingsAudio.MusicVolume))
            {
                _audioEngine.MusicVolume = _appSettings.Audio.MusicVolume / 100f;
                DebounceSave();
            }
            else if (e.PropertyName == nameof(AppSettingsAudio.EffectVolume))
            {
                _audioEngine.EffectVolume = _appSettings.Audio.EffectVolume / 100f;
                DebounceSave();
            }
            else if (e.PropertyName == nameof(AppSettingsAudio.LimiterType))
            {
                _audioEngine.LimiterType = _appSettings.Audio.LimiterType;
                DebounceSave();
            }
        }
        else if (sender is AppSettingsSyncPlayback)
        {
            if (e.PropertyName == nameof(AppSettingsSyncPlayback.BalanceFactor))
            {
                DebounceSave();
            }
        }
        else if (sender is AppSettingsGeneral && e.PropertyName == nameof(AppSettingsGeneral.Theme))
        {
            ApplyTheme();
            try
            {
                _appSettings.Save();
            }
            catch (Exception ex)
            {
                HandleSaveException(ex);
            }
        }
        else
        {
            try
            {
                _appSettings.Save();
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
            _toastManager.CreateToast()
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
                    _appSettings.Save();
                }
                catch (Exception ex)
                {
                    HandleSaveException(ex);
                }
            });
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var obj in _observedSettings)
        {
            obj.PropertyChanged -= OnSettingsChanged;
        }

        _observedSettings.Clear();
        _saveDebounceCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}