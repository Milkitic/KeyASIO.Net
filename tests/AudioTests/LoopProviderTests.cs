using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;

namespace AudioTests;

public sealed class LoopProviderTests
{
    [Fact]
    public async Task LoopSampleProvider_PauseKeepsSourcePosition()
    {
        var cachedAudio = await LoadCachedAudioAsync();
        var source = new CachedAudioProvider(cachedAudio);
        var loop = new LoopSampleProvider(source);
        var buffer = new float[256];

        Assert.Equal(buffer.Length, loop.Read(buffer, 0, buffer.Length));
        var positionBeforePause = source.PlayTime;

        Array.Fill(buffer, 1f);
        loop.IsPaused = true;

        Assert.Equal(QueueMixingSampleProvider.SignalKeepAlive, loop.Read(buffer, 0, buffer.Length));
        Assert.Equal(positionBeforePause, source.PlayTime);
        Assert.All(buffer, sample => Assert.Equal(0f, sample));

        loop.IsPaused = false;

        Assert.Equal(buffer.Length, loop.Read(buffer, 0, buffer.Length));
        Assert.True(source.PlayTime > positionBeforePause);
    }

    [Fact]
    public async Task LoopProviderManager_PauseAndResumeKeepsMixerInputAlive()
    {
        var cachedAudio = await LoadCachedAudioAsync();
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            cachedAudio.WaveFormat.SampleRate,
            cachedAudio.WaveFormat.Channels);
        var mixer = new QueueMixingSampleProvider(waveFormat);
        var manager = new LoopProviderManager();
        var buffer = new float[256];

        manager.Create(1, cachedAudio, mixer, 1f, 0f, BalanceMode.Off);
        Assert.Equal(buffer.Length, mixer.Read(buffer, 0, buffer.Length));

        manager.PauseAll(mixer);
        Array.Fill(buffer, 1f);

        Assert.Equal(buffer.Length, mixer.Read(buffer, 0, buffer.Length));
        Assert.All(buffer, sample => Assert.Equal(0f, sample));

        manager.RecoverAll(mixer);
        Array.Clear(buffer);

        Assert.Equal(buffer.Length, mixer.Read(buffer, 0, buffer.Length));
        Assert.Contains(buffer, sample => sample != 0f);
    }

    private static async Task<CachedAudio> LoadCachedAudioAsync()
    {
        var audioCacheManager = new AudioCacheManager(NullLogger<AudioCacheManager>.Instance);
        var filePath = Path.Combine(AppContext.BaseDirectory, "files", "normal-hitnormal.wav");
        var result = await audioCacheManager.GetOrCreateOrEmptyFromFileAsync(filePath, new WaveFormat(48000, 2));
        return Assert.IsType<CachedAudio>(result.CachedAudio);
    }
}
