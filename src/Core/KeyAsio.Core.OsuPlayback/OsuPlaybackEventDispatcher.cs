using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Core.Audio.Utils;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Core.OsuPlayback;

public sealed class OsuPlaybackEventDispatcher
{
    private readonly LoopProviderManager _loopProviderManager = new();
    private readonly IPlaybackEngine _playbackEngine;
    private readonly ILogger? _logger;

    public OsuPlaybackEventDispatcher(IPlaybackEngine playbackEngine, ILogger? logger = null)
    {
        _playbackEngine = playbackEngine;
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
        _loopProviderManager.RemoveAll(_playbackEngine.EffectMixer);
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
            var cachedAudioProvider = RecyclableSampleProviderFactory.RentCacheProvider(cachedAudio);
            var volumeProvider = RecyclableSampleProviderFactory.RentVolumeProvider(cachedAudioProvider, volume);
            var balanceProvider = RecyclableSampleProviderFactory.RentBalanceProvider(
                volumeProvider,
                sampleEvent.Balance * BalanceFactor,
                BalanceMode,
                AntiClipStrategy.None);

            _playbackEngine.EffectMixer.AddMixerInput(balanceProvider);
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

                if (_loopProviderManager.ShouldRemoveAll((int)controlEvent.LoopChannel))
                {
                    _loopProviderManager.RemoveAll(_playbackEngine.EffectMixer);
                }

                _loopProviderManager.Create((int)controlEvent.LoopChannel,
                    cachedAudio,
                    _playbackEngine.EffectMixer,
                    controlEvent.Volume * HitsoundVolume,
                    controlEvent.Balance,
                    BalanceMode,
                    balanceFactor: BalanceFactor);
                break;
            case ControlEventType.LoopStop:
                _loopProviderManager.Remove((int)controlEvent.LoopChannel, _playbackEngine.EffectMixer);
                break;
            case ControlEventType.Volume:
                _loopProviderManager.ChangeAllVolumes(controlEvent.Volume * HitsoundVolume, volumeFactor: 1);
                break;
            case ControlEventType.Balance:
                _loopProviderManager.ChangeAllBalances(controlEvent.Balance, BalanceFactor);
                break;
        }
    }
}
