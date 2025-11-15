using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio;

internal static class MixingSampleProviderExtension
{
    internal static ISampleProvider? PlayAudio(this MixingSampleProvider mixer, CachedAudio cachedAudio,
        SampleControl? sampleControl)
    {
        PlayAudio(mixer, cachedAudio, sampleControl, out var rootSample);
        return rootSample;
    }

    internal static ISampleProvider? PlayAudio(this MixingSampleProvider mixer, CachedAudio cachedAudio, float volume,
        float balance)
    {
        PlayAudio(mixer, cachedAudio, volume, balance, out var rootSample);
        return rootSample;
    }

    public static async Task<ISampleProvider?> PlayAudio(this MixingSampleProvider mixer, AudioCacheManager audioCacheManager,
        string path,
        SampleControl? sampleControl)
    {
        var waveFormat = new WaveFormat(mixer.WaveFormat.SampleRate, mixer.WaveFormat.Channels);
        await using var fs = File.OpenRead(path);
        var cacheResult = await audioCacheManager.GetOrCreateOrEmptyAsync(path, fs, waveFormat).ConfigureAwait(false);

        PlayAudio(mixer, cacheResult.CachedAudio!, sampleControl, out var rootSample);
        return rootSample;
    }

    public static async Task<ISampleProvider?> PlayAudio(this MixingSampleProvider mixer, AudioCacheManager audioCacheManager,
        string path,
        float volume, float balance)
    {
        var waveFormat = new WaveFormat(mixer.WaveFormat.SampleRate, mixer.WaveFormat.Channels);
        await using var fs = File.OpenRead(path);
        var cacheResult = await audioCacheManager.GetOrCreateOrEmptyAsync(path, fs, waveFormat).ConfigureAwait(false);

        PlayAudio(mixer, cacheResult.CachedAudio!, volume, balance, out var rootSample);
        return rootSample;
    }

    public static void AddMixerInput(this MixingSampleProvider mixer, ISampleProvider input,
        SampleControl? sampleControl, out ISampleProvider rootSample)
    {
        if (sampleControl != null)
        {
            var adjustVolume = input.AddToAdjustVolume(sampleControl.Volume);
            var adjustBalance = adjustVolume.AddToBalanceProvider(sampleControl.Balance);
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

    public static void AddMixerInput(this MixingSampleProvider mixer, ISampleProvider input,
        float volume, float balance, out ISampleProvider rootSample)
    {
        var adjustVolume = volume >= 1 ? input : input.AddToAdjustVolume(volume);
        var adjustBalance = balance == 0 ? adjustVolume : adjustVolume.AddToBalanceProvider(balance);

        rootSample = adjustBalance;
        mixer.AddMixerInput(adjustBalance);
    }

    private static void PlayAudio(MixingSampleProvider mixer, CachedAudio cachedAudio, SampleControl? sampleControl,
        out ISampleProvider? rootSample)
    {
        mixer.AddMixerInput(new CachedAudioSampleProvider(cachedAudio), sampleControl, out rootSample);
    }

    private static void PlayAudio(MixingSampleProvider mixer, CachedAudio cachedAudio, float volume, float balance,
        out ISampleProvider? rootSample)
    {
        mixer.AddMixerInput(new CachedAudioSampleProvider(cachedAudio), volume, balance, out rootSample);
    }

    private static EnhancedVolumeSampleProvider AddToAdjustVolume(this ISampleProvider input, float volume)
    {
        var volumeSampleProvider = new EnhancedVolumeSampleProvider(input)
        {
            Volume = volume
        };
        return volumeSampleProvider;
    }

    private static BalanceSampleProvider AddToBalanceProvider(this ISampleProvider input, float balance)
    {
        var volumeSampleProvider = new BalanceSampleProvider(input)
        {
            Balance = balance
        };
        return volumeSampleProvider;
    }
}