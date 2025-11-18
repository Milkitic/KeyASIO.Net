using System.Buffers;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using KeyAsio.Audio.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio;

public class LoopProviderManager
{
    private readonly Dictionary<int, LoopProvider> _dictionary = new();

    public bool ShouldRemoveAll(int channel)
    {
        return _dictionary.ContainsKey(channel);
    }

    public bool ChangeAllVolumes(float volume, float volumeFactor = 1.25f)
    {
        foreach (var kvp in _dictionary.ToList())
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;
            loopProvider.SetVolume(volume * volumeFactor);
        }

        return true;
    }

    public bool ChangeAllBalances(float balance, float balanceFactor = 1)
    {
        foreach (var kvp in _dictionary.ToList())
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;
            loopProvider.SetBalance(balance * balanceFactor);
        }

        return true;
    }

    public bool ChangeVolume(int slideChannel, float volume, float volumeFactor = 1.25f)
    {
        if (!_dictionary.TryGetValue(slideChannel, out var loopProvider)) return false;
        loopProvider.SetVolume(volume * volumeFactor);
        return true;
    }

    public bool ChangeBalance(int slideChannel, float balance, float balanceFactor = 1)
    {
        if (!_dictionary.TryGetValue(slideChannel, out var loopProvider)) return false;
        loopProvider.SetBalance(balance * balanceFactor);
        return true;
    }

    public bool Remove(int slideChannel, MixingSampleProvider? mixer)
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

    public void PauseAll(MixingSampleProvider? mixer)
    {
        foreach (var kvp in _dictionary)
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;

            loopProvider.RemoveFrom(mixer);
        }
    }

    public void RecoverAll(MixingSampleProvider? mixer)
    {
        foreach (var kvp in _dictionary)
        {
            var channel = kvp.Key;
            var loopProvider = kvp.Value;

            loopProvider.AddTo(mixer);
        }
    }

    public void Create(int slideChannel,
        CachedAudio? cachedAudio,
        MixingSampleProvider mixer,
        float volume,
        float balance,
        float volumeFactor = 1.25f,
        float balanceFactor = 1)
    {
        if (cachedAudio is null) return;

        Remove(slideChannel, mixer);

        var span = cachedAudio.Span;
        if (span.IsEmpty) return;

        var audioDataLength = span.Length;
        var byteArray = ArrayPool<byte>.Shared.Rent(audioDataLength);
        span.CopyTo(byteArray);

        var memoryStream = new MemoryStream(byteArray, 0, audioDataLength);
        var waveStream = new RawSourceWaveStream(memoryStream, cachedAudio.WaveFormat);
        var loopStream = new LoopStream(waveStream);
        var volumeProvider = new EnhancedVolumeSampleProvider(loopStream.ToSampleProvider())
        {
            Volume = volume * volumeFactor
        };
        var balanceProvider =
            new ProfessionalBalanceProvider(volumeProvider, BalanceMode.MidSide, AntiClipStrategy.None)
            {
                Balance = balance * balanceFactor
            };

        var loopProvider = new LoopProvider(balanceProvider, volumeProvider, memoryStream, waveStream, loopStream,
            byteArray);
        _dictionary.Add(slideChannel, loopProvider);
        loopProvider.AddTo(mixer);
    }
}