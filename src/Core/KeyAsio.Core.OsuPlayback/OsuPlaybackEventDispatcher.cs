using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Core.OsuPlayback;

public sealed class OsuPlaybackEventDispatcher
{
    private readonly IOsuEffectPlaybackBus _effectPlaybackBus;
    private readonly ILogger? _logger;

    public OsuPlaybackEventDispatcher(IOsuEffectPlaybackBus effectPlaybackBus, ILogger? logger = null)
    {
        _effectPlaybackBus = effectPlaybackBus;
        _logger = logger;
    }

    public float HitsoundVolume { get; set; } = 1;
    public float SampleVolume { get; set; } = 1;
    public float BalanceFactor { get; set; } = 0.35f;
    public BalanceMode BalanceMode { get; set; } = BalanceMode.ConstantPower;

    public void Dispatch(PlaybackEvent playbackEvent, CachedAudio? cachedAudio)
    {
        switch (playbackEvent)
        {
            case SampleEvent sampleEvent:
                PlaySample(sampleEvent, cachedAudio);
                break;
            case ControlEvent controlEvent:
                PlayControl(controlEvent, cachedAudio);
                break;
        }
    }

    public void ClearLoops()
    {
        _effectPlaybackBus.StopAllLoops();
    }

    private void PlaySample(SampleEvent sampleEvent, CachedAudio? cachedAudio)
    {
        if (cachedAudio == null)
        {
            _logger?.LogWarning("Skip osu sample because cached audio is missing: {Filename}", sampleEvent.Filename);
            return;
        }

        var layerVolume = sampleEvent.Layer == SampleLayer.Sampling ? SampleVolume : HitsoundVolume;
        var volume = sampleEvent.Volume * layerVolume;
        if (sampleEvent.Layer == SampleLayer.Effects)
        {
            volume *= 1.25f;
        }

        try
        {
            _effectPlaybackBus.PlayOneShot(cachedAudio, volume, sampleEvent.Balance, BalanceMode, BalanceFactor);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while playing osu sample: {Filename}", sampleEvent.Filename);
        }
    }

    private void PlayControl(ControlEvent controlEvent, CachedAudio? cachedAudio)
    {
        switch (controlEvent.ControlEventType)
        {
            case ControlEventType.LoopStart:
                if (cachedAudio == null)
                {
                    _logger?.LogWarning("Skip osu loop because cached audio is missing: {Filename}",
                        controlEvent.Filename);
                    return;
                }

                if (_effectPlaybackBus.HasLoop((int)controlEvent.LoopChannel))
                {
                    _effectPlaybackBus.StopAllLoops();
                }

                _effectPlaybackBus.StartLoop((int)controlEvent.LoopChannel,
                    cachedAudio,
                    controlEvent.Volume * HitsoundVolume,
                    controlEvent.Balance,
                    BalanceMode,
                    BalanceFactor);
                break;
            case ControlEventType.LoopStop:
                _effectPlaybackBus.StopLoop((int)controlEvent.LoopChannel);
                break;
            case ControlEventType.Volume:
                _effectPlaybackBus.ChangeAllLoopVolumes(controlEvent.Volume * HitsoundVolume);
                break;
            case ControlEventType.Balance:
                _effectPlaybackBus.ChangeAllLoopBalances(controlEvent.Balance, BalanceFactor);
                break;
        }
    }
}
