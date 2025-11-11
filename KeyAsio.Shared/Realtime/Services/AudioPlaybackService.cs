using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Audio;
using KeyAsio.Shared.Models;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave.SampleProviders;
using BalanceSampleProvider = KeyAsio.Shared.Audio.BalanceSampleProvider;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioPlaybackService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(AudioPlaybackService));
    private readonly LoopProviders _loopProviders = new();

    public void PlayEffectsAudio(CachedSound? cachedSound, float volume, float balance, AppSettings appSettings)
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
            SharedViewModel.Instance.AudioEngine?.EffectMixer.AddMixerInput(
                new BalanceSampleProvider(
                        new EnhancedVolumeSampleProvider(new SeekableCachedSoundSampleProvider(cachedSound))
                        { Volume = volume }
                    )
                { Balance = balance }
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurs while playing audio.", true);
        }

        Logger.Debug($"Play {Path.GetFileNameWithoutExtension(cachedSound.SourcePath)}; " +
                     $"Vol. {volume}; " +
                     $"Bal. {balance}");
    }

    public void PlayLoopAudio(CachedSound? cachedSound, ControlNode controlNode, AppSettings appSettings)
    {
        var rootMixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        if (rootMixer == null)
        {
            Logger.Warn("RootMixer is null, stop adding cache.");
            return;
        }

        var volume = appSettings.RealtimeOptions.IgnoreLineVolumes ? 1 : controlNode.Volume;

        if (controlNode.ControlType == ControlType.StartSliding)
        {
            if (_loopProviders.ShouldRemoveAll(controlNode.SlideChannel))
            {
                _loopProviders.RemoveAll(rootMixer);
            }

            try
            {
                _loopProviders.Create(controlNode, cachedSound, rootMixer, volume, 0, balanceFactor: 0);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurs while playing looped audio.", true);
            }
        }
        else if (controlNode.ControlType == ControlType.StopSliding)
        {
            _loopProviders.Remove(controlNode.SlideChannel, rootMixer);
        }
        else if (controlNode.ControlType == ControlType.ChangeVolume)
        {
            _loopProviders.ChangeAllVolumes(volume);
        }
    }

    public void ClearAllLoops(MixingSampleProvider? mixer = null)
    {
        var m = mixer ?? SharedViewModel.Instance.AudioEngine?.EffectMixer;
        _loopProviders.RemoveAll(m);
    }
}