using System;
using System.Threading.Tasks;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Gui.Realtime.Tracks;

public class SelectSongTrack
{
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(SelectSongTrack));
    private readonly object _instanceLock = new();

    private FadeInOutSampleProvider? _fadeInOutSampleProvider;
    private VolumeSampleProvider? _volumeSampleProvider;
    private MyAudioFileReader? _audioFileReader;
    private WdlResamplingSampleProvider? _resampler;
    private TimingSampleProvider? _timingSampleProvider;

    private MixingSampleProvider? Mixer => SharedViewModel.Instance.AudioEngine?.MusicMixer;
    private WaveFormat? WaveFormat => SharedViewModel.Instance.AudioEngine?.WaveFormat;

    public void PlaySingleAudio(string? path, float volume, int playTime, int fadeInMilliseconds = 1000)
    {
        if (Mixer is null || WaveFormat is null) return;

        lock (_instanceLock)
        {
            if (_volumeSampleProvider is not null) return;
            if (_audioFileReader is not null) return;

            var audioFileReader = _audioFileReader = new MyAudioFileReader(path);
            var resampler = _resampler = new WdlResamplingSampleProvider(audioFileReader, WaveFormat.SampleRate);
            var volumeSampleProvider = _volumeSampleProvider = new VolumeSampleProvider(resampler) { Volume = volume };
            var timingSampleProvider = _timingSampleProvider = new TimingSampleProvider(volumeSampleProvider);
            var fadeInOutSampleProvider = _fadeInOutSampleProvider = new FadeInOutSampleProvider(timingSampleProvider);
            RepositionAndFadeIn(audioFileReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
            _timingSampleProvider.Updated += (oldTime, newTime) =>
            {
                if (audioFileReader.CurrentTime >= audioFileReader.TotalTime.Add(TimeSpan.FromMilliseconds(50)))
                {
                    RepositionAndFadeIn(audioFileReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
                }
            };
            try
            {
                Mixer.AddMixerInput(fadeInOutSampleProvider);
            }
            catch (Exception e)
            {
            }
        }
    }

    public async void StopCurrentMusic(int fadeOutMilliseconds = 500)
    {
        VolumeSampleProvider? volumeSampleProvider;
        MyAudioFileReader? audioFileReader;
        FadeInOutSampleProvider? fadeInOutSampleProvider;
        lock (_instanceLock)
        {
            volumeSampleProvider = _volumeSampleProvider;
            audioFileReader = _audioFileReader;
            fadeInOutSampleProvider = _fadeInOutSampleProvider;
            if (audioFileReader is null || volumeSampleProvider is null || fadeInOutSampleProvider is null)
                return;
            _audioFileReader = null;
            _volumeSampleProvider = null;
            _fadeInOutSampleProvider = null;
        }

        fadeInOutSampleProvider.BeginFadeOut(fadeOutMilliseconds);
        await Task.Delay(fadeOutMilliseconds);
        Mixer?.RemoveMixerInput(fadeInOutSampleProvider);
        await audioFileReader.DisposeAsync();
    }

    private static void RepositionAndFadeIn(WaveStream waveStream, int playTime,
        FadeInOutSampleProvider fadeInOutSampleProvider, int fadeInMilliseconds)
    {
        //waveStream.Position = (long)(waveStream.Length * 0.95);
        if (playTime == -1)
        {
            waveStream.Position = (long)(waveStream.Length * 0.4);
        }
        else
        {
            waveStream.CurrentTime = TimeSpan.FromMilliseconds(playTime);
        }

        fadeInOutSampleProvider.BeginFadeIn(fadeInMilliseconds);
    }
}