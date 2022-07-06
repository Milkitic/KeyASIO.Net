using System;
using System.Threading.Tasks;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
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

    public SelectSongTrack()
    {
    }

    private AudioPlaybackEngine? AudioPlaybackEngine => SharedViewModel.Instance.AudioPlaybackEngine;
    public void PlaySingleAudio(string? path, float volume, int playTime, int fadeInMilliseconds = 2000)
    {
        if (AudioPlaybackEngine is null) return;

        lock (_instanceLock)
        {
            if (_volumeSampleProvider is not null) return;
            if (_audioFileReader is not null) return;

            _audioFileReader = new MyAudioFileReader(path);
            if (playTime == -1)
            {
                _audioFileReader.Position = (long)(_audioFileReader.Length * 0.4);
            }
            else
            {
                _audioFileReader.CurrentTime = TimeSpan.FromMilliseconds(playTime);
            }

            _resampler = new WdlResamplingSampleProvider(_audioFileReader, AudioPlaybackEngine.WaveFormat.SampleRate);
            _volumeSampleProvider = new VolumeSampleProvider(_resampler) { Volume = volume };
            _fadeInOutSampleProvider = new FadeInOutSampleProvider(_volumeSampleProvider, true);
            _fadeInOutSampleProvider.BeginFadeIn(fadeInMilliseconds);
            AudioPlaybackEngine?.RootMixer.AddMixerInput(_fadeInOutSampleProvider);
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
        AudioPlaybackEngine?.RootMixer.RemoveMixerInput(fadeInOutSampleProvider);
        await audioFileReader.DisposeAsync();
    }
}