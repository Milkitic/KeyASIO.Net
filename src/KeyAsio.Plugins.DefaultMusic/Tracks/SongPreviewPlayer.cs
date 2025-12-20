using Coosu.Beatmap;
using KeyAsio.Audio;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.Utils;
using KeyAsio.Audio.Wave;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Plugins.DefaultMusic.Tracks;

public class SongPreviewPlayer
{
    private readonly Lock _instanceLock = new();

    private AudioFileReader? _audioFileReader;
    private EnhancedFadeInOutSampleProvider? _fadeInOutSampleProvider;
    private LowPassSampleProvider? _lowPassSampleProvider;

    private readonly ILogger<SongPreviewPlayer> _logger;
    private readonly AppSettings _appSettings;
    private readonly IAudioEngine _audioEngine;

    public SongPreviewPlayer(ILogger<SongPreviewPlayer> logger, AppSettings appSettings, IAudioEngine audioEngine)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioEngine = audioEngine;
    }

    private IMixingSampleProvider? Mixer => _audioEngine.MusicMixer as IMixingSampleProvider;
    private WaveFormat? WaveFormat => _audioEngine.EngineWaveFormat;

    public async Task Play(OsuFile osuFile, string? path, int playTime, int fadeInMilliseconds = 1000)
    {
        if (!_appSettings.Sync.EnableMixSync) return;
        if (Mixer is null || WaveFormat is null || path is null) return;

        AudioFileReader? audioFileReader;
        EnhancedFadeInOutSampleProvider? fadeInOutSampleProvider = null;
        lock (_instanceLock)
        {
            if (_audioFileReader is not null) return;

            try
            {
                audioFileReader = _audioFileReader = new AudioFileReader(path);
                var builder = new SampleProviderBuilder(audioFileReader);
                if (audioFileReader.WaveFormat.Channels == 1)
                {
                    builder.AddSampleProvider(k => new MonoToStereoSampleProvider(k));
                }

                if (audioFileReader.WaveFormat.SampleRate != WaveFormat.SampleRate)
                {
                    builder.AddSampleProvider(k => new WdlResamplingSampleProvider(k, WaveFormat.SampleRate));
                }

                var notifyingSampleProvider
                    = builder.AddSampleProvider(k => new NotifyingSampleProvider(k));
                _lowPassSampleProvider =
                    builder.AddSampleProvider(k => new LowPassSampleProvider(k, WaveFormat.SampleRate, 16000));
                fadeInOutSampleProvider = _fadeInOutSampleProvider =
                    builder.AddSampleProvider(k => new EnhancedFadeInOutSampleProvider(k));

                notifyingSampleProvider.Sample += async (_, _) =>
                {
                    if (audioFileReader.CurrentTime >= audioFileReader.TotalTime.Add(-TimeSpan.FromMilliseconds(50)))
                    {
                        await RepositionAndFadeIn(audioFileReader, playTime, fadeInOutSampleProvider,
                            fadeInMilliseconds);
                    }
                };

                try
                {
                    Mixer.AddMixerInput(_fadeInOutSampleProvider);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Preview with warning: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Preview error: {osuFile}", true);
                if (fadeInOutSampleProvider != null) Mixer.RemoveMixerInput(fadeInOutSampleProvider);
                return;
            }
        }

        await RepositionAndFadeIn(audioFileReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
    }

    public async Task StopCurrentMusic(int fadeOutMilliseconds = 500)
    {
        AudioFileReader? audioFileReader;
        EnhancedFadeInOutSampleProvider? fadeInOutSampleProvider;
        lock (_instanceLock)
        {
            audioFileReader = _audioFileReader;
            fadeInOutSampleProvider = _fadeInOutSampleProvider;
            if (audioFileReader is null || fadeInOutSampleProvider is null)
                return;
            _audioFileReader = null;
            _fadeInOutSampleProvider = null;
        }

        await FadeAsync(fadeInOutSampleProvider, fadeOutMilliseconds, false);
        Mixer?.RemoveMixerInput(fadeInOutSampleProvider);
        await audioFileReader.DisposeAsync();
    }

    public void StartLowPass(int fadeMilliseconds, int targetFrequency)
    {
        var lowPass = _lowPassSampleProvider;
        if (lowPass is null) return;
        Task.Run(() =>
        {
            var frequency = lowPass.Frequency;
            var sw = HighPrecisionTimer.StartNew();
            while (sw.ElapsedMilliseconds < fadeMilliseconds)
            {
                var ratio = sw.ElapsedMilliseconds / (double)fadeMilliseconds;
                var val = frequency + (targetFrequency - frequency) * ratio;
                lowPass.SetFrequency((int)val);
                Thread.Sleep(10);
            }

            lowPass.SetFrequency(targetFrequency);
        });
    }

    public async Task PauseCurrentMusic()
    {
        AudioFileReader? audioFileReader;
        EnhancedFadeInOutSampleProvider? fadeInOutSampleProvider;
        lock (_instanceLock)
        {
            audioFileReader = _audioFileReader;
            fadeInOutSampleProvider = _fadeInOutSampleProvider;
            if (audioFileReader is null || fadeInOutSampleProvider is null)
                return;
        }

        Mixer?.RemoveMixerInput(fadeInOutSampleProvider);
    }

    public async Task RecoverCurrentMusic()
    {
        AudioFileReader? audioFileReader;
        EnhancedFadeInOutSampleProvider? fadeInOutSampleProvider;
        lock (_instanceLock)
        {
            audioFileReader = _audioFileReader;
            fadeInOutSampleProvider = _fadeInOutSampleProvider;
            if (audioFileReader is null || fadeInOutSampleProvider is null)
                return;
        }

        Mixer?.AddMixerInput(fadeInOutSampleProvider);
    }

    private static async ValueTask RepositionAndFadeIn(WaveStream waveStream, int playTime,
        EnhancedFadeInOutSampleProvider fadeInOutSampleProvider, int fadeInMilliseconds)
    {
        if (playTime < 0)
        {
            waveStream.Position = (long)(waveStream.Length * 0.4);
        }
        else
        {
            waveStream.CurrentTime = TimeSpan.FromMilliseconds(playTime);
        }

        await FadeAsync(fadeInOutSampleProvider, fadeInMilliseconds, true);
    }

    private static async ValueTask FadeAsync(EnhancedFadeInOutSampleProvider fadeInOutSampleProvider,
        int fadeMilliseconds, bool isFadeIn)
    {
        if (isFadeIn)
        {
            fadeInOutSampleProvider.BeginFadeIn(fadeMilliseconds);
        }
        else
        {
            fadeInOutSampleProvider.BeginFadeOut(fadeMilliseconds);
        }

        await Task.Delay(fadeMilliseconds);
    }
}