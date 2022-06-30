using System;
using System.IO;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Gui.Waves;

internal sealed class LoopProvider : IDisposable
{
    private readonly BalanceSampleProvider _balanceProvider;
    private readonly VolumeSampleProvider _volumeProvider;
    private readonly MemoryStream _memoryStream;
    private readonly RawSourceWaveStream _waveStream;
    private readonly LoopStream _loopStream;

    public LoopProvider(BalanceSampleProvider balanceProvider,
        VolumeSampleProvider volumeProvider,
        MemoryStream memoryStream,
        RawSourceWaveStream waveStream,
        LoopStream loopStream)
    {
        _balanceProvider = balanceProvider;
        _volumeProvider = volumeProvider;
        _memoryStream = memoryStream;
        _waveStream = waveStream;
        _loopStream = loopStream;
    }

    public void SetBalance(float balance)
    {
        _balanceProvider.Balance = balance;
    }

    public void SetVolume(float volume)
    {
        _volumeProvider.Volume = volume;
    }

    public void RemoveFrom(MixingSampleProvider? mixer)
    {
        mixer?.RemoveMixerInput(_balanceProvider);
    }

    public void Dispose()
    {
        _loopStream.Dispose();
        _waveStream.Dispose();
        _memoryStream.Dispose();
    }
}