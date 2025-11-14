using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioPlaybackService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(AudioPlaybackService));
    private readonly LoopProviders _loopProviders = new();
    private readonly SharedViewModel _sharedViewModel;

    public AudioPlaybackService(SharedViewModel sharedViewModel)
    {
        _sharedViewModel = sharedViewModel;
    }

    public void PlayEffectsAudio(CachedAudio? cachedSound, float volume, float balance, AppSettings appSettings)
    {
        if (cachedSound is null)
        {
            Logger.Warn("Fail to play: CachedSound not found");
            return;
        }

        if (appSettings.RealtimeOptions.IgnoreLineVolumes)
        {
            volume = 1;
        }

        balance *= appSettings.RealtimeOptions.BalanceFactor;

        try
        {
            _sharedViewModel.AudioEngine.EffectMixer.AddMixerInput(
                new BalanceSampleProvider(
                        new EnhancedVolumeSampleProvider(new SeekableCachedAudioSampleProvider(cachedSound))
                        { Volume = volume }
                    )
                { Balance = balance }
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurs while playing audio.", true);
        }

        Logger.Debug($"Play {Path.GetFileNameWithoutExtension(cachedSound.SourceHash)}; " +
                     $"Vol. {volume}; " +
                     $"Bal. {balance}");
    }

    public void PlayLoopAudio(CachedAudio? cachedSound, ControlNode controlNode, AppSettings appSettings)
    {
        var rootMixer = _sharedViewModel.AudioEngine?.EffectMixer;
        if (rootMixer == null)
        {
            Logger.Warn("RootMixer is null, stop adding cache.");
            return;
        }

        var volume = appSettings.RealtimeOptions.IgnoreLineVolumes ? 1 : controlNode.Volume;

        if (controlNode.ControlType == ControlType.StartSliding)
        {
            if (_loopProviders.ShouldRemoveAll((int)controlNode.SlideChannel))
            {
                _loopProviders.RemoveAll(rootMixer);
            }

            try
            {
                _loopProviders.Create((int)controlNode.SlideChannel, cachedSound, rootMixer, volume, 0, balanceFactor: 0);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurs while playing looped audio.", true);
            }
        }
        else if (controlNode.ControlType == ControlType.StopSliding)
        {
            _loopProviders.Remove((int)controlNode.SlideChannel, rootMixer);
        }
        else if (controlNode.ControlType == ControlType.ChangeVolume)
        {
            _loopProviders.ChangeAllVolumes(volume);
        }
    }

    public void ClearAllLoops(MixingSampleProvider? mixer = null)
    {
        var m = mixer ?? _sharedViewModel.AudioEngine?.EffectMixer;
        _loopProviders.RemoveAll(m);
    }
}