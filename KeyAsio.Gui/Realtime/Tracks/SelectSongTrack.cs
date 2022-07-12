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
using Milki.Extensions.MixPlayer.NAudioExtensions;
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
    private VolumeSampleProvider? _volumeSampleProvider;
    private SampleControl? _sampleControl;
    private LowPassSampleProvider? _lowPassSampleProvider;

    private MixingSampleProvider? Mixer => SharedViewModel.Instance.AudioEngine?.MusicMixer;
    private WaveFormat? WaveFormat => SharedViewModel.Instance.AudioEngine?.WaveFormat;

    public async void PlaySingleAudio(OsuFile osuFile, string? path, int playTime, int fadeInMilliseconds = 1000)
    {
        if (!ConfigurationFactory.GetConfiguration<AppSettings>().RealtimeOptions.EnableMusicFunctions) return;
        if (Mixer is null || WaveFormat is null) return;

        SmartWaveReader? smartWaveReader;
        VolumeSampleProvider? volumeSampleProvider = null;
        SampleControl sampleControl;
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
                volumeSampleProvider = _volumeSampleProvider = new VolumeSampleProvider(biQuadSampleProvider)
                {
                    Volume = 0
                };
                sampleControl = _sampleControl = new SampleControl();
                _notifyingSampleProvider.Sample += async (_, _) =>
                {
                    if (smartWaveReader.CurrentTime >= smartWaveReader.TotalTime.Add(-TimeSpan.FromMilliseconds(50)))
                    {
                        await RepositionAndFadeIn(smartWaveReader, playTime, volumeSampleProvider, fadeInMilliseconds, sampleControl);
                    }
                };
                try
                {
                    Mixer.AddMixerInput(volumeSampleProvider);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Preview with warning: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Preview error: {osuFile}", true);
                Mixer.RemoveMixerInput(volumeSampleProvider);
                return;
            }
        }

        await RepositionAndFadeIn(smartWaveReader, playTime, volumeSampleProvider, fadeInMilliseconds, sampleControl);
    }

    public async void StopCurrentMusic(int fadeOutMilliseconds = 500)
    {
        SmartWaveReader? smartWaveReader;
        VolumeSampleProvider? volumeSampleProvider;
        SampleControl? sampleControl;
        lock (_instanceLock)
        {
            smartWaveReader = _smartWaveReader;
            volumeSampleProvider = _volumeSampleProvider;
            sampleControl = _sampleControl;
            if (smartWaveReader is null || volumeSampleProvider is null || sampleControl is null)
                return;
            _smartWaveReader = null;
            _volumeSampleProvider = null;
            _sampleControl = null;
        }

        await FadeAsync(volumeSampleProvider, fadeOutMilliseconds, false, sampleControl);
        Mixer?.RemoveMixerInput(volumeSampleProvider);
        await smartWaveReader.DisposeAsync();
    }

    private static async ValueTask RepositionAndFadeIn(WaveStream waveStream, int playTime,
        VolumeSampleProvider fadeInOutSampleProvider, int fadeInMilliseconds, SampleControl sampleControl)
    {
        if (playTime == -1)
        {
            waveStream.Position = (long)(waveStream.Length * 0.4);
        }
        else
        {
            waveStream.CurrentTime = TimeSpan.FromMilliseconds(playTime);
        }

        await FadeAsync(fadeInOutSampleProvider, fadeInMilliseconds, true, sampleControl);
    }

    public void StartLowPass(int fadeMilliseconds)
    {
        var lowPass = _lowPassSampleProvider;
        if (lowPass is null) return;
        Task.Run(() =>
        {
            var currentVol = lowPass.Frequency;
            var targetVol = 800;

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

    private static async ValueTask FadeAsync(VolumeSampleProvider volumeSampleProvider, int fadeMilliseconds, bool isFadeIn, SampleControl sampleControl)
    {
        await Task.Run(() =>
        {
            var currentVol = volumeSampleProvider.Volume;
            var targetVol = isFadeIn ? 1 : 0f;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < fadeMilliseconds)
            {
                var ratio = sw.ElapsedMilliseconds / (double)fadeMilliseconds;
                var val = currentVol + (targetVol - currentVol) * ratio;
                volumeSampleProvider.Volume = (float)val;
                Thread.Sleep(100);
            }

            volumeSampleProvider.Volume = targetVol;
        });
    }
}