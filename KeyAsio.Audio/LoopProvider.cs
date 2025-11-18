using System.Buffers;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using KeyAsio.Audio.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio;

internal sealed class LoopProvider : IDisposable
{
    private readonly ProfessionalBalanceProvider _balanceProvider;
    private readonly EnhancedVolumeSampleProvider _volumeProvider;
    private readonly MemoryStream _memoryStream;
    private readonly RawSourceWaveStream _waveStream;
    private readonly LoopStream _loopStream;
    private readonly byte[] _byteArray;
    private bool _isAdded;

    public LoopProvider(ProfessionalBalanceProvider balanceProvider,
        EnhancedVolumeSampleProvider volumeProvider,
        MemoryStream memoryStream,
        RawSourceWaveStream waveStream,
        LoopStream loopStream,
        byte[] byteArray)
    {
        _balanceProvider = balanceProvider;
        _volumeProvider = volumeProvider;
        _memoryStream = memoryStream;
        _waveStream = waveStream;
        _loopStream = loopStream;
        _byteArray = byteArray;
    }

    public void SetBalance(float balance)
    {
        _balanceProvider.Balance = balance;
    }

    public void SetVolume(float volume)
    {
        _volumeProvider.Volume = volume;
    }

    public void AddTo(MixingSampleProvider? mixer)
    {
        if (_isAdded) return;
        mixer?.AddMixerInput(_balanceProvider);
        _isAdded = true;
    }

    public void RemoveFrom(MixingSampleProvider? mixer)
    {
        mixer?.RemoveMixerInput(_balanceProvider);
        _isAdded = false;
    }

    public void Dispose()
    {
        try
        {
            _loopStream.Dispose();
            _waveStream.Dispose();
            _memoryStream.Dispose();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(_byteArray);
        }
    }
}