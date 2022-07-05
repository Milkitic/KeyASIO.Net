using System;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Gui;

public class SelectSongTrack
{
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(SelectSongTrack));
    private VolumeSampleProvider _volumeSampleProvider;
    private MyAudioFileReader? _audioFileReader;
    private WdlResamplingSampleProvider? _resampler;

    public SelectSongTrack()
    {
    }

    private AudioPlaybackEngine? AudioPlaybackEngine => SharedViewModel.Instance.AudioPlaybackEngine;
    public void PlaySingleAudio(string? path, float volume, int playTime)
    {
        if (AudioPlaybackEngine == null) return;

        _audioFileReader = new MyAudioFileReader(path);
        _resampler = new WdlResamplingSampleProvider(_audioFileReader, AudioPlaybackEngine.WaveFormat.SampleRate);
        _volumeSampleProvider = new VolumeSampleProvider(_resampler) { Volume = volume };
        if (playTime == -1)
        {
            _audioFileReader.Position = (long)(_audioFileReader.Length * 0.4);
        }
        else
        {
            _audioFileReader.CurrentTime = TimeSpan.FromMilliseconds(playTime);
        }

        AudioPlaybackEngine?.RootMixer.AddMixerInput(_volumeSampleProvider);
    }

    public void StopMusic()
    {
        AudioPlaybackEngine?.RootMixer.RemoveMixerInput(_volumeSampleProvider);
        _audioFileReader?.Dispose();
    }
}