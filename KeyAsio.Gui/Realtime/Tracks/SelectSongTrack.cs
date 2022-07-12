using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Coosu.Beatmap;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Waves;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Gui.Realtime.Tracks;

// Todo: will write new FadingInOutSampleProvider
public class SelectSongTrack
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(SelectSongTrack));
    private readonly object _instanceLock = new();

    private SmartWaveReader? _smartWaveReader;
    private WdlResamplingSampleProvider? _resampler;
    private NotifyingSampleProvider? _notifyingSampleProvider;
    private FadeInOutSampleProvider? _fadeInOutSampleProvider;
    private LowPassSampleProvider? _lowPassSampleProvider;

    private MixingSampleProvider? Mixer => SharedViewModel.Instance.AudioEngine?.MusicMixer;
    private WaveFormat? WaveFormat => SharedViewModel.Instance.AudioEngine?.WaveFormat;

    public async void PlaySingleAudio(OsuFile osuFile, string? path, int playTime, int fadeInMilliseconds = 1000)
    {
        if (!ConfigurationFactory.GetConfiguration<AppSettings>().RealtimeOptions.EnableMusicFunctions) return;
        if (Mixer is null || WaveFormat is null) return;

        SmartWaveReader? smartWaveReader;
        FadeInOutSampleProvider? fadeInOutSampleProvider = null;
        lock (_instanceLock)
        {
            if (_smartWaveReader is not null) return;

            try
            {
                smartWaveReader = _smartWaveReader = new SmartWaveReader(path);
                ISampleProvider sampleProvider = smartWaveReader.WaveFormat.Channels == 1
                    ? new MonoToStereoSampleProvider(smartWaveReader)
                    : smartWaveReader;
                NotifyingSampleProvider notifyingSampleProvider;
                if (smartWaveReader.WaveFormat.SampleRate != WaveFormat.SampleRate)
                {
                    var resampler = _resampler = new WdlResamplingSampleProvider(sampleProvider, WaveFormat.SampleRate);
                    notifyingSampleProvider = _notifyingSampleProvider = new NotifyingSampleProvider(resampler);
                }
                else
                {
                    notifyingSampleProvider = _notifyingSampleProvider = new NotifyingSampleProvider(sampleProvider);
                }


                var biQuadSampleProvider = _lowPassSampleProvider = new LowPassSampleProvider(notifyingSampleProvider,
                    SharedViewModel.Instance.AudioEngine!.WaveFormat.SampleRate, 16000);
                fadeInOutSampleProvider = _fadeInOutSampleProvider = new FadeInOutSampleProvider(biQuadSampleProvider);
                _notifyingSampleProvider.Sample += async (_, _) =>
                {
                    if (smartWaveReader.CurrentTime >= smartWaveReader.TotalTime.Add(-TimeSpan.FromMilliseconds(50)))
                    {
                        await RepositionAndFadeIn(smartWaveReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
                    }
                };
                try
                {
                    Mixer.AddMixerInput(fadeInOutSampleProvider);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Preview with warning: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Preview error: {osuFile}", true);
                Mixer.RemoveMixerInput(fadeInOutSampleProvider);
                return;
            }
        }

        await RepositionAndFadeIn(smartWaveReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
    }

    public async void StopCurrentMusic(int fadeOutMilliseconds = 500)
    {
        SmartWaveReader? smartWaveReader;
        FadeInOutSampleProvider? fadeInOutSampleProvider;
        lock (_instanceLock)
        {
            smartWaveReader = _smartWaveReader;
            fadeInOutSampleProvider = _fadeInOutSampleProvider;
            if (smartWaveReader is null || fadeInOutSampleProvider is null)
                return;
            _smartWaveReader = null;
            _fadeInOutSampleProvider = null;
        }

        await FadeAsync(fadeInOutSampleProvider, fadeOutMilliseconds, false);
        Mixer?.RemoveMixerInput(fadeInOutSampleProvider);
        await smartWaveReader.DisposeAsync();
    }

    public void StartLowPass(int fadeMilliseconds, int targetVol)
    {
        var lowPass = _lowPassSampleProvider;
        if (lowPass is null) return;
        Task.Run(() =>
        {
            var currentVol = lowPass.Frequency;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < fadeMilliseconds)
            {
                var ratio = sw.ElapsedMilliseconds / (double)fadeMilliseconds;
                var val = currentVol + (targetVol - currentVol) * ratio;
                lowPass.SetFrequency((int)val);
                Thread.Sleep(10);
            }

            lowPass.SetFrequency(targetVol);
        });
    }

    private static async ValueTask RepositionAndFadeIn(WaveStream waveStream, int playTime,
        FadeInOutSampleProvider fadeInOutSampleProvider, int fadeInMilliseconds)
    {
        if (playTime == -1)
        {
            waveStream.Position = (long)(waveStream.Length * 0.4);
        }
        else
        {
            waveStream.CurrentTime = TimeSpan.FromMilliseconds(playTime);
        }

        await FadeAsync(fadeInOutSampleProvider, fadeInMilliseconds, true);
    }

    private static async ValueTask FadeAsync(FadeInOutSampleProvider volumeSampleProvider, int fadeMilliseconds, bool isFadeIn)
    {
        if (isFadeIn)
        {
            volumeSampleProvider.BeginFadeIn(fadeMilliseconds);
        }
        else
        {
            volumeSampleProvider.BeginFadeOut(fadeMilliseconds);
        }

        await Task.Delay(fadeMilliseconds);
        //await Task.Run(() =>
        //{
        //    var currentVol = volumeSampleProvider.Volume;
        //    var targetVol = isFadeIn ? 1 : 0f;

        //    var sw = Stopwatch.StartNew();
        //    while (sw.ElapsedMilliseconds < fadeMilliseconds)
        //    {
        //        var ratio = sw.ElapsedMilliseconds / (double)fadeMilliseconds;
        //        var val = currentVol + (targetVol - currentVol) * ratio;
        //        volumeSampleProvider.Volume = (float)val;
        //        Thread.Sleep(10);
        //    }

        //    volumeSampleProvider.Volume = targetVol;
        //});
    }
}