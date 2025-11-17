using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using KeyAsio.MemoryReading.Logging;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioPlaybackService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(AudioPlaybackService));
    private readonly LoopProviderManager _loopProviderManager = new();
    private readonly AudioEngine _audioEngine;
    private readonly AppSettings _appSettings;

    public AudioPlaybackService(AudioEngine audioEngine, AppSettings appSettings)
    {
        _audioEngine = audioEngine;
        _appSettings = appSettings;
    }

    public void PlayEffectsAudio(CachedAudio? cachedAudio, float volume, float balance)
    {
        if (cachedAudio is null)
        {
            Logger.Warn("Fail to play: CachedSound not found");
            return;
        }

        if (_appSettings.RealtimeOptions.IgnoreLineVolumes)
        {
            volume = 1;
        }

        balance *= _appSettings.RealtimeOptions.BalanceFactor;

        try
        {
            var seekableCachedAudioSampleProvider = new SeekableCachedAudioProvider(cachedAudio);
            var volumeSampleProvider = new EnhancedVolumeSampleProvider(seekableCachedAudioSampleProvider)
            {
                Volume = volume
            };
            var balanceProvider = new ProfessionalBalanceProvider(volumeSampleProvider, BalanceMode.MidSide, AntiClipStrategy.None)
            {
                Balance = balance
            };
            _audioEngine.EffectMixer.AddMixerInput(balanceProvider);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurs while playing audio.", true);
        }

        Logger.Debug($"Play {Path.GetFileNameWithoutExtension(cachedAudio.SourceHash)}; " +
                     $"Vol. {volume}; " +
                     $"Bal. {balance}");
    }

    public void PlayLoopAudio(CachedAudio? cachedAudio, ControlNode controlNode)
    {
        var rootMixer = _audioEngine.EffectMixer;
        //if (rootMixer == null)
        //{
        //    Logger.Warn("RootMixer is null, stop adding cache.");
        //    return;
        //}

        var volume = _appSettings.RealtimeOptions.IgnoreLineVolumes ? 1 : controlNode.Volume;

        if (controlNode.ControlType == ControlType.StartSliding)
        {
            if (_loopProviderManager.ShouldRemoveAll((int)controlNode.SlideChannel))
            {
                _loopProviderManager.RemoveAll(rootMixer);
            }

            try
            {
                _loopProviderManager.Create((int)controlNode.SlideChannel, cachedAudio, rootMixer, volume, 0, balanceFactor: 0);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurs while playing looped audio.", true);
            }
        }
        else if (controlNode.ControlType == ControlType.StopSliding)
        {
            _loopProviderManager.Remove((int)controlNode.SlideChannel, rootMixer);
        }
        else if (controlNode.ControlType == ControlType.ChangeVolume)
        {
            _loopProviderManager.ChangeAllVolumes(volume);
        }
    }

    public void ClearAllLoops(MixingSampleProvider? mixingSampleProvider = null)
    {
        mixingSampleProvider ??= _audioEngine.EffectMixer;
        _loopProviderManager.RemoveAll(mixingSampleProvider);
    }
}