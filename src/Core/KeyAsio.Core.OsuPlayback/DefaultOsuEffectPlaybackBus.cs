using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Core.Audio.Utils;

namespace KeyAsio.Core.OsuPlayback;

public sealed class DefaultOsuEffectPlaybackBus : IOsuEffectPlaybackBus
{
    private readonly IMixingSampleProvider _effectMixer;
    private readonly LoopProviderManager _loopProviderManager = new();

    public DefaultOsuEffectPlaybackBus(IMixingSampleProvider effectMixer)
    {
        _effectMixer = effectMixer;
    }

    public void PlayOneShot(CachedAudio cachedAudio, float volume, float balance, BalanceMode balanceMode,
        float balanceFactor)
    {
        var cachedAudioProvider = RecyclableSampleProviderFactory.RentCacheProvider(cachedAudio);
        var volumeProvider = RecyclableSampleProviderFactory.RentVolumeProvider(cachedAudioProvider, volume);
        var balanceProvider = RecyclableSampleProviderFactory.RentBalanceProvider(
            volumeProvider,
            balance * balanceFactor,
            balanceMode,
            AntiClipStrategy.None);

        _effectMixer.AddMixerInput(balanceProvider);
    }

    public bool HasLoop(int channel)
    {
        return _loopProviderManager.ShouldRemoveAll(channel);
    }

    public void StartLoop(int channel, CachedAudio cachedAudio, float volume, float balance, BalanceMode balanceMode,
        float balanceFactor)
    {
        _loopProviderManager.Create(channel,
            cachedAudio,
            _effectMixer,
            volume,
            balance,
            balanceMode,
            balanceFactor: balanceFactor);
    }

    public void StopLoop(int channel)
    {
        _loopProviderManager.Remove(channel, _effectMixer);
    }

    public void StopAllLoops()
    {
        _loopProviderManager.RemoveAll(_effectMixer);
    }

    public void ChangeAllLoopVolumes(float volume)
    {
        _loopProviderManager.ChangeAllVolumes(volume, volumeFactor: 1);
    }

    public void ChangeAllLoopBalances(float balance, float balanceFactor)
    {
        _loopProviderManager.ChangeAllBalances(balance, balanceFactor);
    }
}
