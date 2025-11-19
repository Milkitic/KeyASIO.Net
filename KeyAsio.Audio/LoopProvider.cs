using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio;

internal sealed class LoopProvider : IDisposable
{
    private readonly SeekableCachedAudioProvider _sourceProvider;
    private readonly LoopSampleProvider _loopWrapper;
    private readonly EnhancedVolumeSampleProvider _volumeProvider;
    private readonly ProfessionalBalanceProvider _balanceProvider;

    private MixingSampleProvider? _baseMixer;

    public LoopProvider(CachedAudio cachedAudio,
        float initialVolume,
        float initialBalance)
    {
        _sourceProvider = new SeekableCachedAudioProvider(cachedAudio);
        _loopWrapper = new LoopSampleProvider(_sourceProvider);
        _volumeProvider = new EnhancedVolumeSampleProvider(_loopWrapper)
        {
            Volume = initialVolume
        };
        _balanceProvider = new ProfessionalBalanceProvider(_volumeProvider,
            BalanceMode.MidSide, AntiClipStrategy.None)
        {
            Balance = initialBalance
        };
    }

    public void SetBalance(float balance)
    {
        _balanceProvider.Balance = balance;
    }

    public void SetVolume(float volume)
    {
        _volumeProvider.Volume = volume;
    }

    public void AddTo(MixingSampleProvider? mixer)
    {
        if (_baseMixer != null) return;
        mixer?.AddMixerInput(_balanceProvider);
        _baseMixer = mixer;
    }

    public void RemoveFrom(MixingSampleProvider? mixer)
    {
        if (_baseMixer == null) return;
        mixer?.RemoveMixerInput(_balanceProvider);
        _baseMixer = null;
    }

    public void Dispose()
    {
        _baseMixer = null;
    }
}