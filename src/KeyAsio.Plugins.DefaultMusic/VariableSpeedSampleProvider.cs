using NAudio.Wave;

namespace KeyAsio.Plugins.DefaultMusic;

public class VariableSpeedSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _sourceProvider;
    private readonly SoundTouch.SoundTouch _soundTouch;
    private readonly float[] _sourceReadBuffer;
    private readonly float[] _soundTouchReadBuffer;
    private readonly int _channelCount;
    private float _playbackRate = 1.0f;
    private bool _repositionRequested;

    private VariableSpeedOptions? _currentSoundTouchProfile;

    public VariableSpeedSampleProvider(ISampleProvider sourceProvider,
        int readDurationMilliseconds,
        VariableSpeedOptions variableSpeedOptions)
    {
        _soundTouch = new SoundTouch.SoundTouch();
        // explore what the default values are before we change them:

        //var logger = Configuration.Instance.GetLogger<VariableSpeedSampleProvider>();
        //logger?.LogDebug("SoundTouch Version {0}", _soundTouch.VersionString);
        //logger?.LogDebug("Use QuickSeek: {0}", _soundTouch.GetUseQuickSeek());
        //logger?.LogDebug("Use AntiAliasing: {0}", _soundTouch.GetUseAntiAliasing());

        SetSoundTouchProfile(variableSpeedOptions);
        _sourceProvider = sourceProvider;
        _soundTouch.SetSampleRate(WaveFormat.SampleRate);
        _channelCount = WaveFormat.Channels;
        _soundTouch.SetChannels(_channelCount);
        _sourceReadBuffer = new float[(WaveFormat.SampleRate * _channelCount * (long)readDurationMilliseconds) / 1000];
        _soundTouchReadBuffer = new float[_sourceReadBuffer.Length * 10]; // support down to 0.1 speed
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_playbackRate.Equals(0)) // play silence
        {
            for (int n = 0; n < count; n++)
            {
                buffer[offset++] = 0;
            }

            return count;
        }

        if (_repositionRequested)
        {
            _soundTouch.Clear();
            _repositionRequested = false;
        }

        int samplesRead = 0;
        bool reachedEndOfSource = false;
        while (samplesRead < count)
        {
            if (_soundTouch.NumberOfSamplesAvailable == 0)
            {
                var readFromSource = _sourceProvider.Read(_sourceReadBuffer, 0, _sourceReadBuffer.Length);
                if (readFromSource > 0)
                {
                    _soundTouch.PutSamples(_sourceReadBuffer, readFromSource / _channelCount);
                }
                else
                {
                    reachedEndOfSource = true;
                    // we've reached the end, tell SoundTouch we're done
                    _soundTouch.Flush();
                }
            }

            var desiredSampleFrames = (count - samplesRead) / _channelCount;

            var received = _soundTouch.ReceiveSamples(_soundTouchReadBuffer, desiredSampleFrames) * _channelCount;
            // use loop instead of Array.Copy due to WaveBuffer
            for (int n = 0; n < received; n++)
            {
                buffer[offset + samplesRead++] = _soundTouchReadBuffer[n];
            }

            if (received == 0 && reachedEndOfSource) break;
        }
        return samplesRead;
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate.Equals(value)) return;
            UpdatePlaybackRate(value);
            _playbackRate = value;
        }
    }

    public void SetSoundTouchProfile(VariableSpeedOptions variableSpeedOptions)
    {
        if (_currentSoundTouchProfile != null &&
            !_playbackRate.Equals(1) &&
            variableSpeedOptions.KeepTune != _currentSoundTouchProfile.KeepTune)
        {
            if (variableSpeedOptions.KeepTune)
            {
                _soundTouch.SetRate(1.0f);
                _soundTouch.SetPitchOctaves(0f);
                _soundTouch.SetTempo(_playbackRate);
            }
            else
            {
                _soundTouch.SetTempo(1.0f);
                _soundTouch.SetRate(_playbackRate);
            }
        }
        _currentSoundTouchProfile = variableSpeedOptions;
        _soundTouch.SetUseAntiAliasing(variableSpeedOptions.UseAntiAliasing);
        _soundTouch.SetUseQuickSeek(variableSpeedOptions.UseQuickSeek);
    }

    public void Reposition()
    {
        _repositionRequested = true;
    }

    public void Dispose()
    {
        _soundTouch.Dispose();
    }

    private void UpdatePlaybackRate(float value)
    {
        if (value.Equals(0)) return;
        if (_currentSoundTouchProfile == null) return;

        if (_currentSoundTouchProfile.KeepTune)
        {
            _soundTouch.SetTempo(value);
        }
        else
        {
            _soundTouch.SetRate(value);
        }
    }
}