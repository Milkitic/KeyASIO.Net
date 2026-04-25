using KeyAsio.Plugins.Abstractions.OsuMemory;
using NAudio.Wave;

namespace KeyAsio.Shared.Sync.Services;

public sealed class ComboGrowthAudioGuard : IDisposable
{
    private const int RequiredTimingScansAfterKeyDown = 2;
    private const int ValidationPollIntervalMs = 1;

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
    public (int Combo, int Score, int HitErrorIndex, long TimingScanGeneration) SnapshotBaselines()
        => (_syncSessionContext.Combo,
            _syncSessionContext.Score,
            _syncSessionContext.HitErrors.Index,
            _syncSessionContext.TimingScanGeneration);

    public void Track(
        ISampleProvider provider,
        int comboBaseline,
        int scoreBaseline,
        int hitErrorIndexBaseline,
        long timingScanGenerationBaseline)
    {
        if (!_syncSessionContext.IsStarted || _syncSessionContext.OsuStatus != OsuMemoryStatus.Playing)
        {
            return;
        }

        int revertDelay = _appSettings.Sync.Scanning.RevertHitsoundDelay;
        if (revertDelay <= 0)
        {
            // Explicit opt-out: do not run rollback validation or stop the speculative hitsound.
            return;
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);

        lock (_lock) _pendingValidations.Add(cts);

        _ = ValidateAfterDelayAsync(
            provider,
            comboBaseline,
            scoreBaseline,
            hitErrorIndexBaseline,
            timingScanGenerationBaseline,
            revertDelay,
            cts);
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
        long timingScanGenerationBaseline,
        int delayMs,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delayMs, cts.Token).ConfigureAwait(false);

            var requiredTimingScanGeneration = timingScanGenerationBaseline + RequiredTimingScansAfterKeyDown;
            var maxExtraWaitMs = Math.Max(ValidationPollIntervalMs, _appSettings.Sync.Scanning.TimingScanInterval * 3);
            var waitStartedAt = Environment.TickCount64;

            while (!IsDisposedOrCanceled(cts) &&
                   !HasConfirmedHit(comboBaseline, scoreBaseline, hitErrorIndexBaseline) &&
                   _syncSessionContext.TimingScanGeneration < requiredTimingScanGeneration &&
                   Environment.TickCount64 - waitStartedAt < maxExtraWaitMs)
            {
                await Task.Delay(ValidationPollIntervalMs, cts.Token).ConfigureAwait(false);
            }

            if (IsDisposedOrCanceled(cts))
            {
                return;
            }

            if (!_syncSessionContext.IsStarted || _syncSessionContext.OsuStatus != OsuMemoryStatus.Playing)
            {
                return;
            }

            if (_syncSessionContext.TimingScanGeneration < requiredTimingScanGeneration)
            {
                return;
            }

            if (!HasConfirmedHit(comboBaseline, scoreBaseline, hitErrorIndexBaseline))
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

    private bool IsDisposedOrCanceled(CancellationTokenSource cts)
        => cts.IsCancellationRequested || _disposeCts.IsCancellationRequested;

    private bool HasConfirmedHit(int comboBaseline, int scoreBaseline, int hitErrorIndexBaseline)
    {
        bool comboChanged = _syncSessionContext.Combo > comboBaseline;
        bool scoreChanged = _syncSessionContext.Score > scoreBaseline;
        bool hitErrorsChanged = _syncSessionContext.HitErrors.Index > hitErrorIndexBaseline;
        return comboChanged || scoreChanged || hitErrorsChanged;
    }
}
