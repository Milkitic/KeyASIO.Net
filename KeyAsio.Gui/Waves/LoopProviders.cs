using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coosu.Beatmap.Extensions.Playback;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Gui.Waves;

internal class LoopProviders
{
    private readonly Dictionary<SlideChannel, LoopProvider> _dictionary = new();

    public bool ShouldRemoveAll(SlideChannel channel)
    {
        return _dictionary.ContainsKey(channel);
    }

    public bool ChangeAllVolumes(float volume)
    {
        foreach (var kvp in _dictionary.ToList())
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;
            loopProvider.SetVolume(volume);
        }
        return true;
    }

    public bool ChangeAllBalances(float balance)
    {
        foreach (var kvp in _dictionary.ToList())
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;
            loopProvider.SetBalance(balance);
        }

        return true;
    }

    public bool ChangeVolume(SlideChannel slideChannel, float volume)
    {
        if (!_dictionary.TryGetValue(slideChannel, out var loopProvider)) return false;
        loopProvider.SetVolume(volume);
        return true;
    }

    public bool ChangeBalance(SlideChannel slideChannel, float balance)
    {
        if (!_dictionary.TryGetValue(slideChannel, out var loopProvider)) return false;
        loopProvider.SetBalance(balance);
        return true;
    }

    public bool Remove(SlideChannel slideChannel, MixingSampleProvider? mixer)
    {
        if (_dictionary.TryGetValue(slideChannel, out var loopProvider))
        {
            loopProvider.RemoveFrom(mixer);
            loopProvider.Dispose();
            return _dictionary.Remove(slideChannel);
        }

        return false;
    }

    public void RemoveAll(MixingSampleProvider? mixer)
    {
        foreach (var kvp in _dictionary.ToList())
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;

            loopProvider.RemoveFrom(mixer);
            loopProvider.Dispose();
            _dictionary.Remove(channel);
        }
    }

    public void Create(ControlNode controlNode,
        CachedSound? cachedSound,
        MixingSampleProvider mixer,
        float balanceFactor = 1)
    {
        if (cachedSound is null) return;

        var slideChannel = controlNode.SlideChannel;
        Remove(slideChannel, mixer);

        var byteArray = new byte[cachedSound.AudioData.Length * sizeof(float)];
        Buffer.BlockCopy(cachedSound.AudioData, 0, byteArray, 0, byteArray.Length);

        var memoryStream = new MemoryStream(byteArray);
        var waveStream = new RawSourceWaveStream(memoryStream, cachedSound.WaveFormat);
        var loopStream = new LoopStream(waveStream);
        var volumeProvider = new VolumeSampleProvider(loopStream.ToSampleProvider())
        {
            Volume = controlNode.Volume
        };
        var balanceProvider = new BalanceSampleProvider(volumeProvider)
        {
            Balance = controlNode.Balance * balanceFactor
        };

        _dictionary.Add(slideChannel, new LoopProvider(balanceProvider, volumeProvider, memoryStream, waveStream, loopStream));
        mixer?.AddMixerInput(balanceProvider);
    }
}