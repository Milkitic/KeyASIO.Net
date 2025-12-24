using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Core.Audio.Utils;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.Services;

public class SfxPlaybackService
{
    private readonly LoopProviderManager _loopProviderManager = new();
    private readonly ILogger<SfxPlaybackService> _logger;
    private readonly AudioEngine _audioEngine;
    private readonly AppSettings _appSettings;

    public SfxPlaybackService(ILogger<SfxPlaybackService> logger, AudioEngine audioEngine, AppSettings appSettings)
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

        if (cachedAudio.SourceHash != null && cachedAudio.SourceHash.StartsWith("internal://dynamic/"))
        {
            try
            {
                var filename = cachedAudio.SourceHash.Substring("internal://dynamic/".Length);

                // 创建一次性军鼓生成器
                var provider = new SnareDrumOneShotProvider(_audioEngine.EffectMixer.WaveFormat);

                // 简单的参数随机化
                var random = Random.Shared;

                // 1. 基频微调 (±5%)
                // 模拟击打位置不同导致的音高微差
                float freqOffset = (random.NextSingle() * 2f - 1f) * 0.05f;
                provider.FundamentalFrequency *= (1f + freqOffset);

                // 2. 混合比例微调 (±10%)
                // 模拟响弦共振的不同
                float mixOffset = (random.NextSingle() * 2f - 1f) * 0.1f;
                provider.SnareMixLevel = Math.Clamp(provider.SnareMixLevel + mixOffset, 0f, 1f);
                provider.SnapMixLevel = Math.Clamp(provider.SnapMixLevel - mixOffset * 0.5f, 0f, 1f);

                // 3. 衰减时间微调 (±10%)
                // 模拟力度的不同导致余音长度变化
                float decayOffset = (random.NextSingle() * 2f - 1f) * 0.1f;
                provider.SnareDecayDuration *= (1f + decayOffset);
                provider.SnapDecayDuration *= (1f + decayOffset);

                // 根据文件名做一些简单的预设调整
                if (filename.Contains("soft", StringComparison.OrdinalIgnoreCase))
                {
                    provider.InitialSnapGain *= 0.8f;
                    provider.InitialSnareGain *= 0.7f;
                    provider.FundamentalFrequency *= 0.9f;
                }
                else if (filename.Contains("drum", StringComparison.OrdinalIgnoreCase))
                {
                    provider.FundamentalFrequency *= 0.8f;
                    provider.SnareMixLevel *= 0.5f;
                    provider.SnapMixLevel *= 1.2f;
                }
                // normal-hitnormal 保持原样

                var volumeProvider = RecyclableSampleProviderFactory.RentVolumeProvider(provider, volume);
                var balanceProvider = RecyclableSampleProviderFactory.RentBalanceProvider(volumeProvider, balance,
                    BalanceMode.MidSide, AntiClipStrategy.None);

                _audioEngine.EffectMixer.AddMixerInput(balanceProvider);
                _logger.LogTrace("Play Dynamic: {Key} (Freq: {Freq:F1})", cachedAudio.SourceHash, provider.FundamentalFrequency);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing dynamic audio");
            }
            return;
        }

        if (_appSettings.Sync.Filters.IgnoreLineVolumes)
        {
            volume = 1;
        }

        balance *= _appSettings.Sync.Playback.BalanceFactor;

        try
        {
            var cachedAudioProvider = RecyclableSampleProviderFactory.RentCacheProvider(cachedAudio);
            var volumeProvider = RecyclableSampleProviderFactory.RentVolumeProvider(cachedAudioProvider, volume);
            var balanceProvider = RecyclableSampleProviderFactory.RentBalanceProvider(volumeProvider, balance,
                BalanceMode.MidSide, AntiClipStrategy.None); // 削波处理交给MasterLimiterProvider

            _audioEngine.EffectMixer.AddMixerInput(balanceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while playing audio.");
        }

        _logger.LogTrace("Play {File}; Vol. {Volume}; Bal. {Balance}", cachedAudio.SourceHash, volume, balance);
    }

    public void PlayLoopAudio(CachedAudio cachedAudio, ControlNode controlNode)
    {
        var effectMixer = _audioEngine.EffectMixer;
        var volume = _appSettings.Sync.Filters.IgnoreLineVolumes ? 1 : controlNode.Volume;

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
                _logger.LogError(ex, "Error occurs while playing looped audio.");
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

    public void ClearAllLoops(IMixingSampleProvider? mixingSampleProvider = null)
    {
        mixingSampleProvider ??= _audioEngine.EffectMixer;
        _loopProviderManager.RemoveAll(mixingSampleProvider);
    }

    public void DispatchPlayback(PlaybackInfo playbackInfo, float? overrideVolume = null)
    {
        var cachedAudio = playbackInfo.CachedAudio;
        var hitsoundNode = playbackInfo.HitsoundNode;
        if (hitsoundNode is PlayableNode playableNode)
        {
            float volume;
            if (_appSettings.Sync.Filters.IgnoreLineVolumes)
            {
                volume = 1;
            }
            else
            {
                if (overrideVolume != null)
                    volume = overrideVolume.Value;
                else if (playableNode.PlayablePriority == PlayablePriority.Effects)
                    volume = playableNode.Volume * 1.25f;
                else
                    volume = playableNode.Volume;
            }

            PlayEffectsAudio(cachedAudio, volume, playableNode.Balance);
        }
        else
        {
            var controlNode = (ControlNode)hitsoundNode;
            PlayLoopAudio(cachedAudio!, controlNode);
        }
    }
}