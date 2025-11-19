using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using NAudio.Wave;

namespace KeyAsio.Audio;

internal static class EnhancedMixingSampleProviderExtension
{
    extension(EnhancedMixingSampleProvider mixer)
    {
        [Obsolete]
        public ISampleProvider? PlayAudio(CachedAudio cachedAudio, SampleControl? sampleControl)
        {
            PlayAudio(mixer, cachedAudio, sampleControl, out var rootSample);
            return rootSample;
        }
        
        [Obsolete]
        public ISampleProvider? PlayAudio(CachedAudio cachedAudio, float volume, float balance)
        {
            PlayAudio(mixer, cachedAudio, volume, balance, out var rootSample);
            return rootSample;
        }
        
        [Obsolete]
        public async Task<ISampleProvider?> PlayAudio(AudioCacheManager audioCacheManager, string path,
            SampleControl? sampleControl)
        {
            var waveFormat = new WaveFormat(mixer.WaveFormat.SampleRate, mixer.WaveFormat.Channels);
            var cacheResult = await audioCacheManager.GetOrCreateOrEmptyFromFileAsync(path, waveFormat)
                .ConfigureAwait(false);

            PlayAudio(mixer, cacheResult.CachedAudio!, sampleControl, out var rootSample);
            return rootSample;
        }
        
        [Obsolete]
        public async Task<ISampleProvider?> PlayAudio(AudioCacheManager audioCacheManager, string path, float volume,
            float balance)
        {
            var waveFormat = new WaveFormat(mixer.WaveFormat.SampleRate, mixer.WaveFormat.Channels);
            var cacheResult = await audioCacheManager.GetOrCreateOrEmptyFromFileAsync(path, waveFormat)
                .ConfigureAwait(false);

            PlayAudio(mixer, cacheResult.CachedAudio!, volume, balance, out var rootSample);
            return rootSample;
        }
        
        [Obsolete]
        public void AddMixerInput(ISampleProvider input, SampleControl? sampleControl, out ISampleProvider rootSample)
        {
            if (sampleControl != null)
            {
                var adjustVolume = AddToAdjustVolume(input, sampleControl.Volume);
                var adjustBalance = AddToBalanceProvider(adjustVolume, sampleControl.Balance);
                sampleControl.VolumeChanged ??= f => adjustVolume.Volume = f;
                sampleControl.BalanceChanged ??= f => adjustBalance.Balance = f;
                rootSample = adjustBalance;
                mixer.AddMixerInput(adjustBalance);
            }
            else
            {
                rootSample = input;
                mixer.AddMixerInput(input);
            }
        }
        
        [Obsolete]
        public void AddMixerInput(ISampleProvider input, float volume, float balance, out ISampleProvider rootSample)
        {
            var adjustVolume = volume >= 1 ? input : AddToAdjustVolume(input, volume);
            var adjustBalance = balance == 0 ? adjustVolume : AddToBalanceProvider(adjustVolume, balance);

            rootSample = adjustBalance;
            mixer.AddMixerInput(adjustBalance);
        }
    }
    
    [Obsolete]
    private static void PlayAudio(EnhancedMixingSampleProvider mixer, CachedAudio cachedAudio, SampleControl? sampleControl,
        out ISampleProvider? rootSample)
    {
        mixer.AddMixerInput(new CachedAudioProvider(cachedAudio), sampleControl, out rootSample);
    }
    
    [Obsolete]
    private static void PlayAudio(EnhancedMixingSampleProvider mixer, CachedAudio cachedAudio, float volume, float balance,
        out ISampleProvider? rootSample)
    {
        mixer.AddMixerInput(new CachedAudioProvider(cachedAudio), volume, balance, out rootSample);
    }

    [Obsolete]
    private static EnhancedVolumeSampleProvider AddToAdjustVolume(ISampleProvider input, float volume)
    {
        var volumeSampleProvider = new EnhancedVolumeSampleProvider(input)
        {
            ExcludeFromPool = true,
            Volume = volume
        };
        return volumeSampleProvider;
    }
    
    [Obsolete]
    private static ProfessionalBalanceProvider AddToBalanceProvider(ISampleProvider input, float balance)
    {
        var balanceProvider = new ProfessionalBalanceProvider(input,
            BalanceMode.MidSide, AntiClipStrategy.None) // 由 MasterLimiterProvider 统一处理防削波
        {
            ExcludeFromPool = true,
            Balance = balance
        };
        return balanceProvider;
    }
}