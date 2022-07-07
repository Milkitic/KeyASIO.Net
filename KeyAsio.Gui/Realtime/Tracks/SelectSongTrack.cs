using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Coosu.Beatmap;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
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

    private MyAudioFileReader? _audioFileReader;
    private WdlResamplingSampleProvider? _resampler;
    private TimingSampleProvider? _timingSampleProvider;
    private VolumeSampleProvider? _volumeSampleProvider;
    private SampleControl? _sampleControl;

    private MixingSampleProvider? Mixer => SharedViewModel.Instance.AudioEngine?.MusicMixer;
    private WaveFormat? WaveFormat => SharedViewModel.Instance.AudioEngine?.WaveFormat;

    public async void PlaySingleAudio(OsuFile osuFile, string? path, int playTime, int fadeInMilliseconds = 1000)
    {
        if (Mixer is null || WaveFormat is null) return;

        MyAudioFileReader? audioFileReader;
        VolumeSampleProvider? volumeSampleProvider = null;
        SampleControl sampleControl;
        lock (_instanceLock)
        {
            if (_audioFileReader is not null) return;

            try
            {
                audioFileReader = _audioFileReader = new MyAudioFileReader(path);
                var resampler = _resampler = new WdlResamplingSampleProvider(audioFileReader, WaveFormat.SampleRate);
                var timingSampleProvider = _timingSampleProvider = new TimingSampleProvider(resampler);
                volumeSampleProvider = _volumeSampleProvider = new VolumeSampleProvider(timingSampleProvider)
                {
                    Volume = 0
                };
                sampleControl = _sampleControl = new SampleControl();
                _timingSampleProvider.Updated += async (oldTime, newTime) =>
                {
                    if (audioFileReader.CurrentTime >= audioFileReader.TotalTime.Add(-TimeSpan.FromMilliseconds(50)))
                    {
                        await RepositionAndFadeIn(audioFileReader, playTime, volumeSampleProvider, fadeInMilliseconds, sampleControl);
                    }
                };
                try
                {
                    Mixer.AddMixerInput(volumeSampleProvider);
                }
                catch (Exception e)
                {
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, $"Preview error: {osuFile}");
                Mixer.RemoveMixerInput(volumeSampleProvider);
                return;
            }
        }

        await RepositionAndFadeIn(audioFileReader, playTime, volumeSampleProvider, fadeInMilliseconds, sampleControl);
    }

    public async void StopCurrentMusic(int fadeOutMilliseconds = 500)
    {
        MyAudioFileReader? audioFileReader;
        VolumeSampleProvider? volumeSampleProvider;
        SampleControl? sampleControl;
        lock (_instanceLock)
        {
            audioFileReader = _audioFileReader;
            volumeSampleProvider = _volumeSampleProvider;
            sampleControl = _sampleControl;
            if (audioFileReader is null || volumeSampleProvider is null || sampleControl is null)
                return;
            _audioFileReader = null;
            _volumeSampleProvider = null;
            _sampleControl = null;
        }

        await FadeAsync(volumeSampleProvider, fadeOutMilliseconds, false, sampleControl);
        Mixer?.RemoveMixerInput(volumeSampleProvider);
        await audioFileReader.DisposeAsync();
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
                Thread.Sleep(20);
            }

            volumeSampleProvider.Volume = targetVol;
        });
    }
}