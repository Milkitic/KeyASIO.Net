using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.Limiters;

/// <summary>
/// 主限制器
/// </summary>
public class MasterLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _lookaheadFrames;
    private readonly float[] _lookaheadBuffer;
    private readonly float[] _peakBuffer;     // 存储每帧的峰值

    private float _thresholdLinear;
    private float _ceilingLinear;
    private float _attackTime;
    private float _releaseTime;

    private float _gainReduction = 1.0f;
    private float _attackCoeff;
    private float _releaseCoeff;

    private int _writePos;
    private int _readPos;
    private float _currentMaxPeak;   // 当前窗口最大峰值


    public MasterLimiterProvider(
        ISampleProvider source,
        float thresholdDb = -0.5f,
        float ceilingDb = -0.1f,
        float attackMs = 0.1f,
        float releaseMs = 50f,
        float lookaheadMs = 2f)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        _lookaheadFrames = Math.Max(1, (int)(source.WaveFormat.SampleRate * lookaheadMs / 1000f));
        _lookaheadBuffer = new float[_lookaheadFrames * _channels];
        _peakBuffer = new float[_lookaheadFrames];

        ThresholdDb = thresholdDb;
        CeilingDb = ceilingDb;
        AttackTime = attackMs;
        ReleaseTime = releaseMs;

        _writePos = 0;
        _readPos = 0;
        _currentMaxPeak = 0f;
    }

    public bool IsEnabled { get; set; } = true;
    public int ClipPreventionCount { get; private set; }
    public float MaxPeakDetected { get; private set; }
    public float CurrentGainReduction => 1.0f - _gainReduction;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float ThresholdDb
    {
        get => LinearToDb(_thresholdLinear);
        set => _thresholdLinear = DbToLinear(value);
    }

    public float CeilingDb
    {
        get => LinearToDb(_ceilingLinear);
        set => _ceilingLinear = DbToLinear(value);
    }

    public float AttackTime
    {
        get => _attackTime;
        set
        {
            _attackTime = value;
            UpdateCoefficients();
        }
    }

    public float ReleaseTime
    {
        get => _releaseTime;
        set
        {
            _releaseTime = value;
            UpdateCoefficients();
        }
    }

    private void UpdateCoefficients()
    {
        float sampleRate = WaveFormat.SampleRate;
        _attackCoeff = MathF.Exp(-1000f / (_attackTime * sampleRate));
        _releaseCoeff = MathF.Exp(-1000f / (_releaseTime * sampleRate));
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (!IsEnabled) return samplesRead;
        if (samplesRead == 0) return 0;

        Process(buffer, offset, samplesRead);
        return samplesRead;
    }

    private void Process(float[] buffer, int offset, int count)
    {
        int frameCount = count / _channels;

        for (int frame = 0; frame < frameCount; frame++)
        {
            int bufferIndex = offset + (frame * _channels);
            int writeIndex = _writePos * _channels;
            int readIndex = _readPos * _channels;

            // 计算输入帧的峰值
            float inputPeak = 0f;
            for (int ch = 0; ch < _channels; ch++)
            {
                float sample = buffer[bufferIndex + ch];
                _lookaheadBuffer[writeIndex + ch] = sample;
                inputPeak = Math.Max(inputPeak, Math.Abs(sample));
            }

            // 更新滑动窗口最大值
            float oldPeak = _peakBuffer[_writePos];
            _peakBuffer[_writePos] = inputPeak;

            if (inputPeak >= _currentMaxPeak)
            {
                // 新峰值更大，直接更新
                _currentMaxPeak = inputPeak;
            }
            else if (oldPeak >= _currentMaxPeak)
            {
                // 移除的是当前最大值，需要重新扫描
                _currentMaxPeak = 0f;
                for (int i = 0; i < _lookaheadFrames; i++)
                {
                    _currentMaxPeak = Math.Max(_currentMaxPeak, _peakBuffer[i]);
                }
            }
            // 否则保持不变

            MaxPeakDetected = Math.Max(MaxPeakDetected, _currentMaxPeak);

            // 计算目标增益
            float targetGain = 1.0f;
            if (_currentMaxPeak > _thresholdLinear)
            {
                float effectiveThreshold = Math.Min(_thresholdLinear, _ceilingLinear);
                targetGain = effectiveThreshold / _currentMaxPeak;
                ClipPreventionCount++;
            }

            // 平滑增益
            if (targetGain < _gainReduction)
            {
                _gainReduction = targetGain + (_gainReduction - targetGain) * _attackCoeff;
            }
            else
            {
                _gainReduction = targetGain + (_gainReduction - targetGain) * _releaseCoeff;
            }

            // 输出延迟样本
            for (int ch = 0; ch < _channels; ch++)
            {
                float delayedSample = _lookaheadBuffer[readIndex + ch];
                buffer[bufferIndex + ch] = delayedSample * _gainReduction;
            }

            _writePos = (_writePos + 1) % _lookaheadFrames;
            _readPos = (_readPos + 1) % _lookaheadFrames;
        }
    }

    private static float DbToLinear(float db)
    {
        return MathF.Pow(10f, db / 20f);
    }

    private static float LinearToDb(float linear)
    {
        if (linear < 0.00001f) return -100.0f;
        return 20f * MathF.Log10(linear);
    }

    public void ResetStatistics()
    {
        ClipPreventionCount = 0;
        MaxPeakDetected = 0f;
    }
}