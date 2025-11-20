using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using KeyAsio.Audio.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioPlaybackService
{
    private readonly LoopProviderManager _loopProviderManager = new();
    private readonly ILogger<AudioPlaybackService> _logger;
    private readonly AudioEngine _audioEngine;
    private readonly AppSettings _appSettings;

    public AudioPlaybackService(ILogger<AudioPlaybackService> logger, AudioEngine audioEngine, AppSettings appSettings)
    {
        _logger = logger;
        _audioEngine = audioEngine;
        _appSettings = appSettings;
    }

    public void PlayEffectsAudio(CachedAudio? cachedAudio, float volume, float balance)
    {
        if (cachedAudio is null)
        {
            _logger.LogWarning("Fail to play: CachedSound not found");
            return;
        }

        if (_appSettings.RealtimeOptions.IgnoreLineVolumes)
        {
            volume = 1;
        }

        balance *= _appSettings.RealtimeOptions.BalanceFactor;

        try
        {
            var cachedAudioProvider = RecyclableSampleProviderFactory.RentCacheProvider(cachedAudio);
            var volumeProvider = RecyclableSampleProviderFactory.RentVolumeProvider(cachedAudioProvider, volume);
            var balanceProvider = RecyclableSampleProviderFactory.RentBalanceProvider(volumeProvider, balance,
                BalanceMode.MidSide, AntiClipStrategy.None); // 由 MasterLimiterProvider 统一处理防削波

            _audioEngine.EffectMixer.AddMixerInput(balanceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while playing audio.", true);
        }

        _logger.LogTrace("Play {File}; Vol. {Volume}; Bal. {Balance}", cachedAudio.SourceHash, volume, balance);
    }

    public void PlayLoopAudio(CachedAudio cachedAudio, ControlNode controlNode)
    {
        var effectMixer = _audioEngine.EffectMixer;
        var volume = _appSettings.RealtimeOptions.IgnoreLineVolumes ? 1 : controlNode.Volume;

        if (controlNode.ControlType == ControlType.StartSliding)
        {
            if (_loopProviderManager.ShouldRemoveAll((int)controlNode.SlideChannel))
            {
                _loopProviderManager.RemoveAll(effectMixer);
            }

            try
            {
                _loopProviderManager.Create((int)controlNode.SlideChannel, cachedAudio, effectMixer, volume, 0, balanceFactor: 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurs while playing looped audio.", true);
            }
        }
        else if (controlNode.ControlType == ControlType.StopSliding)
        {
            _loopProviderManager.Remove((int)controlNode.SlideChannel, effectMixer);
        }
        else if (controlNode.ControlType == ControlType.ChangeVolume)
        {
            _loopProviderManager.ChangeAllVolumes(volume);
        }
    }

    public void ClearAllLoops(EnhancedMixingSampleProvider? mixingSampleProvider = null)
    {
        mixingSampleProvider ??= _audioEngine.EffectMixer;
        _loopProviderManager.RemoveAll(mixingSampleProvider);
    }
}