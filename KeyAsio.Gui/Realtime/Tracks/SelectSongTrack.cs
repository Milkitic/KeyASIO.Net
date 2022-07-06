using System;
using System.Threading.Tasks;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Waves;
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
    private TimingSampleProvider _timingSampleProvider;

    public SelectSongTrack()
    {
    }

    private AudioEngine? AudioEngine => SharedViewModel.Instance.AudioEngine;
    public void PlaySingleAudio(string? path, float volume, int playTime, int fadeInMilliseconds = 2000)
    {
        if (AudioEngine is null) return;

        lock (_instanceLock)
        {
            if (_volumeSampleProvider is not null) return;
            if (_audioFileReader is not null) return;

            var audioFileReader = _audioFileReader = new MyAudioFileReader(path);
            _resampler = new WdlResamplingSampleProvider(_audioFileReader, AudioEngine.WaveFormat.SampleRate);
            _volumeSampleProvider = new VolumeSampleProvider(_resampler) { Volume = volume };
            _timingSampleProvider = new TimingSampleProvider(_volumeSampleProvider);
            var fadeInOutSampleProvider = _fadeInOutSampleProvider = new FadeInOutSampleProvider(_timingSampleProvider, true);
            RepositionAndFadeIn(audioFileReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
            _timingSampleProvider.Updated += (oldTime, newTime) =>
            {
                if (newTime >= audioFileReader.TotalTime)
                {
                    RepositionAndFadeIn(audioFileReader, playTime, fadeInOutSampleProvider, fadeInMilliseconds);
                }
            };
            AudioEngine?.EffectMixer.AddMixerInput(_fadeInOutSampleProvider);
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
        AudioEngine?.RootMixer.RemoveMixerInput(fadeInOutSampleProvider);
        await audioFileReader.DisposeAsync();
    }

    private static void RepositionAndFadeIn(WaveStream waveStream, int playTime,
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

        fadeInOutSampleProvider.BeginFadeIn(fadeInMilliseconds);
    }
}