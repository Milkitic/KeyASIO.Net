using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;

namespace KeyAsio.Core.OsuPlayback;

public interface IOsuEffectPlaybackBus
{
    void PlayOneShot(CachedAudio cachedAudio, float volume, float balance, BalanceMode balanceMode, float balanceFactor);

    bool HasLoop(int channel);

    void StartLoop(int channel, CachedAudio cachedAudio, float volume, float balance, BalanceMode balanceMode,
        float balanceFactor);

    void StopLoop(int channel);

    void StopAllLoops();

    void ChangeAllLoopVolumes(float volume);

    void ChangeAllLoopBalances(float balance, float balanceFactor);
}
