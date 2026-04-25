using KeyAsio.Plugins.Abstractions.OsuMemory;
using NAudio.Wave;

namespace KeyAsio.Shared.Sync.Services;

public sealed class ComboGrowthAudioGuard : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<CancellationTokenSource> _pendingValidations = new(8);
    private readonly SyncSessionContext _syncSessionContext;
    private readonly SfxPlaybackService _sfxPlaybackService;
    private readonly AppSettings _appSettings;
    private readonly CancellationTokenSource _disposeCts = new();

    public ComboGrowthAudioGuard(
        SyncSessionContext syncSessionContext,
        SfxPlaybackService sfxPlaybackService,
        AppSettings appSettings)
    {
        _syncSessionContext = syncSessionContext;
        _sfxPlaybackService = sfxPlaybackService;
        _appSettings = appSettings;
    }

    /// <summary>
    /// Captures the current validation baselines. Call this at the very start of a key-press
    /// callback, before any processing, so snapshots are taken before osu! updates memory.
    /// </summary>
    public (int Combo, int Score, int HitErrorIndex) SnapshotBaselines()
        => (_syncSessionContext.Combo, _syncSessionContext.Score, _syncSessionContext.HitErrors.Index);

    public void Track(ISampleProvider provider, int comboBaseline, int scoreBaseline, int hitErrorIndexBaseline)
    {
        if (!_syncSessionContext.IsStarted || _syncSessionContext.OsuStatus != OsuMemoryStatus.Playing)
        {
            return;
        }

        int revertDelay = Math.Max(1, _appSettings.Sync.Scanning.RevertHitsoundDelay);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);

        lock (_lock) _pendingValidations.Add(cts);

        _ = ValidateAfterDelayAsync(provider, comboBaseline, scoreBaseline, hitErrorIndexBaseline, revertDelay, cts);
    }

    public void Clear()
    {
        CancellationTokenSource[] pending;
        lock (_lock)
        {
            pending = _pendingValidations.ToArray();
            _pendingValidations.Clear();
        }

        foreach (var cts in pending)
        {
            cts.Cancel();
        }
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        Clear();
        _disposeCts.Dispose();
    }

    private async Task ValidateAfterDelayAsync(
        ISampleProvider provider,
        int comboBaseline,
        int scoreBaseline,
        int hitErrorIndexBaseline,
        int delayMs,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delayMs, cts.Token).ConfigureAwait(false);

            if (cts.IsCancellationRequested || _disposeCts.IsCancellationRequested)
            {
                return;
            }

            if (!_syncSessionContext.IsStarted || _syncSessionContext.OsuStatus != OsuMemoryStatus.Playing)
            {
                return;
            }

            bool comboChanged = _syncSessionContext.Combo > comboBaseline;
            bool scoreChanged = _syncSessionContext.Score > scoreBaseline;
            bool hitErrorsChanged = _syncSessionContext.HitErrors.Index > hitErrorIndexBaseline;

            if (!comboChanged && !scoreChanged && !hitErrorsChanged)
            {
                _sfxPlaybackService.StopEffectsAudio(provider);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_lock) _pendingValidations.Remove(cts);
            cts.Dispose();
        }
    }
}
