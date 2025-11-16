using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.Limiters;

/// <summary>
/// Provides a lookahead peak limiter for mastering, preventing audio signals
/// from exceeding a specified ceiling.
/// </summary>
/// <remarks>
/// This provider implements <see cref="ISampleProvider"/> and analyzes the peak
/// level of incoming audio using a lookahead buffer. When a peak exceeds the
/// threshold, it applies gain reduction smoothly (based on attack and release times)
/// to ensure the output signal does not surpass the ceiling.
/// </remarks>
public class MasterLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _lookaheadFrames;
    private readonly float[] _lookaheadBuffer;
    private readonly float[] _peakBuffer; // 存储每帧的峰值

    private float _thresholdLinear;
    private float _ceilingLinear;
    private float _attackTime;
    private float _releaseTime;

    private float _gainReduction = 1.0f;
    private float _attackCoeff;
    private float _releaseCoeff;

    private int _writePos;
    private int _readPos;
    private float _currentMaxPeak; // 当前窗口最大峰值

    /// <summary>
    /// Initializes a new instance of the <see cref="MasterLimiterProvider"/> class.
    /// </summary>
    /// <param name="source">The source sample provider to apply the limiter to.</param>
    /// <param name="thresholdDb">The threshold in decibels (dB) at which limiting starts. Default is -0.5 dB.</param>
    /// <param name="ceilingDb">The absolute maximum output level in decibels (dB). Default is -0.1 dB.</param>
    /// <param name="attackMs">The attack time in milliseconds (ms) for the gain reduction. Default is 0.1 ms.</param>
    /// <param name="releaseMs">The release time in milliseconds (ms) for the gain reduction. Default is 50 ms.</param>
    /// <param name="lookaheadMs">The lookahead time in milliseconds (ms) to anticipate peaks. Default is 2 ms.</param>
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

    /// <summary>
    /// Gets or sets a value indicating whether the limiter processing is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if enabled; otherwise, <c>false</c>. When <c>false</c>, audio passes through unmodified (bypassed).
    /// </value>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the number of times the gain reduction was activated to prevent clipping.
    /// </summary>
    /// <remarks>
    /// This is a useful statistic for monitoring how often the limiter is working.
    /// Can be reset using <see cref="ResetStatistics"/>.
    /// </remarks>
    public int ClipPreventionCount { get; private set; }

    /// <summary>
    /// Gets the maximum peak (linear amplitude) detected in the lookahead buffer since the last reset.
    /// </summary>
    /// <remarks>
    /// This value represents the highest peak level <b>before</b> limiting was applied.
    /// Can be reset using <see cref="ResetStatistics"/>.
    /// </remarks>
    public float MaxPeakDetected { get; private set; }

    /// <summary>
    /// Gets the current amount of gain reduction being applied, as a linear scalar.
    /// </summary>
    /// <value>
    /// A value of 0.0 indicates no gain reduction. A value of 0.1 indicates that the
    /// signal is being attenuated by 10% (i.e., multiplied by 0.9).
    /// </value>
    public float CurrentGainReduction => 1.0f - _gainReduction;

    /// <summary>
    /// Gets the WaveFormat of this sample provider.
    /// </summary>
    /// <value>The wave format.</value>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the limiter threshold in decibels (dB).
    /// </summary>
    /// <value>
    /// The threshold (dB) above which gain reduction will be applied.
    /// </value>
    public float ThresholdDb
    {
        get => LinearToDb(_thresholdLinear);
        set => _thresholdLinear = DbToLinear(value);
    }

    /// <summary>
    /// Gets or sets the output ceiling in decibels (dB).
    /// </summary>
    /// <value>
    /// The absolute maximum level (dB) that the output signal will reach.
    /// </value>
    public float CeilingDb
    {
        get => LinearToDb(_ceilingLinear);
        set => _ceilingLinear = DbToLinear(value);
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds (ms).
    /// </summary>
    /// <value>
    /// The time it takes for the limiter to react and apply gain reduction when a peak exceeds the threshold.
    /// </value>
    public float AttackTime
    {
        get => _attackTime;
        set
        {
            _attackTime = value;
            UpdateCoefficients();
        }
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds (ms).
    /// </summary>
    /// <value>
    /// The time it takes for the gain reduction to return to zero after the signal falls below the threshold.
    /// </value>
    public float ReleaseTime
    {
        get => _releaseTime;
        set
        {
            _releaseTime = value;
            UpdateCoefficients();
        }
    }

    /// <summary>
    /// Reads samples from this provider, applying the limiter processing.
    /// </summary>
    /// <param name="buffer">The buffer to fill with samples.</param>
    /// <param name="offset">The offset into the buffer to start writing.</param>
    /// <param name="count">The number of samples requested.</param>
    /// <returns>The number of samples read.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (!IsEnabled) return samplesRead;
        if (samplesRead == 0) return 0;

        Process(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Resets the internal statistics (<see cref="ClipPreventionCount"/> and <see cref="MaxPeakDetected"/>) to zero.
    /// </summary>
    public void ResetStatistics()
    {
        ClipPreventionCount = 0;
        MaxPeakDetected = 0f;
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

    private void UpdateCoefficients()
    {
        float sampleRate = WaveFormat.SampleRate;
        _attackCoeff = MathF.Exp(-1000f / (_attackTime * sampleRate));
        _releaseCoeff = MathF.Exp(-1000f / (_releaseTime * sampleRate));
    }
}