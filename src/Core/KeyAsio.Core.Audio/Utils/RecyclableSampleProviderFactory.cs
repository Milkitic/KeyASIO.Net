using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.Utils;

public static class RecyclableSampleProviderFactory
{
    public static EnhancedVolumeSampleProvider RentVolumeProvider(ISampleProvider source, float volume)
    {
        var provider = SharedPool<EnhancedVolumeSampleProvider>.Rent();
        provider.Source = source;
        provider.Volume = volume;
        return provider;
    }

    public static ProfessionalBalanceProvider RentBalanceProvider(ISampleProvider source, float balance, BalanceMode mode,
        AntiClipStrategy antiClipStrategy)
    {
        var provider = SharedPool<ProfessionalBalanceProvider>.Rent();
        provider.Source = source;
        provider.Balance = balance;
        provider.Mode = mode;
        provider.AntiClipStrategy = antiClipStrategy;
        return provider;
    }

    public static LoopSampleProvider RentLoopProvider(CachedAudioProvider source)
    {
        var provider = SharedPool<LoopSampleProvider>.Rent();
        provider.Source = source;
        return provider;
    }

    public static CachedAudioProvider RentCacheProvider(CachedAudio cachedAudio)
    {
        var provider = SharedPool<CachedAudioProvider>.Rent();
        provider.Initialize(cachedAudio);
        return provider;
    }

    public static void Return(EnhancedVolumeSampleProvider provider)
    {
        SharedPool<EnhancedVolumeSampleProvider>.Return(provider);
    }

    public static void Return(ProfessionalBalanceProvider provider)
    {
        SharedPool<ProfessionalBalanceProvider>.Return(provider);
    }

    public static void Return(LoopSampleProvider provider)
    {
        SharedPool<LoopSampleProvider>.Return(provider);
    }

    public static void Return(CachedAudioProvider provider)
    {
        SharedPool<CachedAudioProvider>.Return(provider);
    }
}