using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Sync.Abstractions;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace KeyAsio.Services;

public sealed record AudioDeviceOperationResult(
    bool IsSuccess,
    DeviceDescription? ActiveDevice,
    Exception? Error = null,
    Exception? RollbackError = null)
{
    public bool WasRolledBack => Error is not null && RollbackError is null;
}

public interface IAudioDeviceOperationCoordinator
{
    Task<AudioDeviceOperationResult> InitializeConfiguredAsync(CancellationToken cancellationToken = default);

    Task<AudioDeviceOperationResult> ApplyAsync(
        DeviceDescription? device,
        int sampleRate,
        CancellationToken cancellationToken = default);

    Task<AudioDeviceOperationResult> ReloadAsync(CancellationToken cancellationToken = default);

    Task<AudioDeviceOperationResult> DeactivateAsync(CancellationToken cancellationToken = default);

    Task<AudioDeviceOperationResult> ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the complete audio-device transition. Operations are serialized and
/// settings are committed only after the requested device is running.
/// </summary>
public sealed class AudioDeviceOperationCoordinator : IAudioDeviceOperationCoordinator, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IAppSettingsPersistence _persistence;
    private readonly IPlaybackEngine _engine;
    private readonly IGameplayAudioCache _gameplayAudio;
    private readonly ILogger<AudioDeviceOperationCoordinator> _logger;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public AudioDeviceOperationCoordinator(
        AppSettings settings,
        IAppSettingsPersistence persistence,
        IPlaybackEngine engine,
        IGameplayAudioCache gameplayAudio,
        ILogger<AudioDeviceOperationCoordinator> logger)
    {
        _settings = settings;
        _persistence = persistence;
        _engine = engine;
        _gameplayAudio = gameplayAudio;
        _logger = logger;
    }

    public Task<AudioDeviceOperationResult> InitializeConfiguredAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            _settings.Audio.PlaybackDevice,
            _settings.Audio.SampleRate,
            persist: false,
            cancellationToken);

    public Task<AudioDeviceOperationResult> ApplyAsync(
        DeviceDescription? device,
        int sampleRate,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(device, sampleRate, persist: true, cancellationToken);

    public Task<AudioDeviceOperationResult> ReloadAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            _settings.Audio.PlaybackDevice,
            _settings.Audio.SampleRate,
            persist: false,
            cancellationToken);

    public Task<AudioDeviceOperationResult> DeactivateAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(null, _settings.Audio.SampleRate, persist: false, cancellationToken);

    public Task<AudioDeviceOperationResult> ClearAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(null, _settings.Audio.SampleRate, persist: true, cancellationToken);

    public void Dispose() => _operationGate.Dispose();

    private async Task<AudioDeviceOperationResult> TransitionAsync(
        DeviceDescription? requestedDevice,
        int requestedSampleRate,
        bool persist,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var savedDevice = _settings.Audio.PlaybackDevice;
            var savedSampleRate = _settings.Audio.SampleRate;
            var rollbackDevice = _engine.CurrentDevice is null
                ? null
                : _engine.CurrentDeviceDescription;
            var rollbackSampleRate = _engine.CurrentDevice is null
                ? savedSampleRate
                : _engine.SourceWaveFormat.SampleRate;

            var commitAttempted = false;
            try
            {
                await StopCurrentDeviceAsync(cancellationToken).ConfigureAwait(false);
                StartRequestedDevice(requestedDevice, requestedSampleRate);

                if (persist)
                {
                    _settings.Audio.PlaybackDevice = requestedDevice;
                    _settings.Audio.SampleRate = requestedSampleRate;
                    commitAttempted = true;
                    _persistence.Save();
                }

                InvalidateGameplayAudio();
                return new AudioDeviceOperationResult(true, _engine.CurrentDeviceDescription);
            }
            catch (Exception error)
            {
                _logger.LogError(error,
                    "Audio transition failed. Device={Device}; SampleRate={SampleRate}",
                    requestedDevice?.FriendlyName ?? "none",
                    requestedSampleRate);

                _settings.Audio.PlaybackDevice = savedDevice;
                _settings.Audio.SampleRate = savedSampleRate;

                List<Exception>? rollbackErrors = null;
                try
                {
                    await StopCurrentDeviceAsync(CancellationToken.None).ConfigureAwait(false);
                    StartRequestedDevice(rollbackDevice, rollbackSampleRate);
                }
                catch (Exception exception)
                {
                    (rollbackErrors ??= []).Add(exception);
                    _logger.LogCritical(exception, "Failed to restore the previous audio device");
                }

                if (commitAttempted)
                {
                    try
                    {
                        _persistence.Save();
                    }
                    catch (Exception exception)
                    {
                        (rollbackErrors ??= []).Add(exception);
                        _logger.LogCritical(exception, "Failed to restore the previous persisted audio settings");
                    }
                }

                InvalidateGameplayAudio();

                Exception? rollbackError = rollbackErrors switch
                {
                    null or { Count: 0 } => null,
                    { Count: 1 } => rollbackErrors[0],
                    _ => new AggregateException("Multiple audio rollback operations failed", rollbackErrors)
                };

                return new AudioDeviceOperationResult(
                    false,
                    _engine.CurrentDeviceDescription,
                    error,
                    rollbackError);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void StartRequestedDevice(DeviceDescription? device, int sampleRate)
    {
        if (device is null)
        {
            return;
        }

        _engine.LimiterType = _settings.Sync.Playback.LimiterType;
        _engine.MainVolume = _settings.Audio.MasterVolume / 100f;
        _engine.MusicVolume = _settings.Audio.MusicVolume / 100f;
        _engine.EffectVolume = _settings.Audio.EffectVolume / 100f;
        _engine.StartDevice(device, new WaveFormat(sampleRate, 2));
    }

    private async Task StopCurrentDeviceAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (_engine.CurrentDevice is null) return;

            try
            {
                _engine.StopDevice();
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                _logger.LogWarning(exception,
                    "Failed to stop the audio device on attempt {Attempt}; retrying",
                    attempt);
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void InvalidateGameplayAudio()
    {
        try
        {
            _gameplayAudio.ClearCaches();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to invalidate gameplay audio caches after a device transition");
        }
    }
}
