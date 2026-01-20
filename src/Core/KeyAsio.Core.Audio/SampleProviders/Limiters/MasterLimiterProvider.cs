using System.Diagnostics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

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
public sealed class MasterLimiterProvider : ISampleProvider
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        Process(buffer, offset, samplesRead);
        return samplesRead;
    }

    private void Process(float[] buffer, int offset, int count)
    {
        int channels = _channels;
        int frameCount = count / _channels;

        float gainReduction = _gainReduction;
        int writePos = _writePos;
        int readPos = _readPos;
        float currentMaxPeak = _currentMaxPeak;

        float thresholdLinear = _thresholdLinear;
        float ceilingLinear = _ceilingLinear;
        float effectiveThreshold = Math.Min(thresholdLinear, ceilingLinear);

        float attackCoeff = _attackCoeff;
        float releaseCoeff = _releaseCoeff;
        int lookaheadFrames = _lookaheadFrames;

        Span<float> lookaheadBuffer = _lookaheadBuffer.AsSpan();
        Span<float> peakBuffer = _peakBuffer.AsSpan();

        for (int frame = 0; frame < frameCount; frame++)
        {
            int bufferIndex = offset + (frame * channels);
            int writeIndex = writePos * channels;
            int readIndex = readPos * channels;

            // 计算输入帧的峰值
            float inputPeak = 0f;
            if (channels == 2)
            {
                float s0 = buffer[bufferIndex];
                float s1 = buffer[bufferIndex + 1];

                lookaheadBuffer[writeIndex] = s0;
                lookaheadBuffer[writeIndex + 1] = s1;

                inputPeak = Math.Max(Math.Abs(s0), Math.Abs(s1));
            }
            else
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = buffer[bufferIndex + ch];
                    lookaheadBuffer[writeIndex + ch] = sample;
                    inputPeak = Math.Max(inputPeak, Math.Abs(sample));
                }
            }

            // 更新滑动窗口最大值
            float oldPeak = peakBuffer[writePos];
            peakBuffer[writePos] = inputPeak;

            if (inputPeak >= currentMaxPeak)
            {
                // 新峰值更大，直接更新
                currentMaxPeak = inputPeak;
            }
            else if (oldPeak >= currentMaxPeak)
            {
                Debug.Assert(oldPeak.Equals(currentMaxPeak));
                // 移除的是当前最大值，需要重新扫描
                // todo: sliding algorithm
                currentMaxPeak = TensorPrimitives.Max(peakBuffer);
            }

            // 计算目标增益
            float targetGain = 1.0f;
            if (currentMaxPeak > thresholdLinear)
            {
                targetGain = effectiveThreshold / currentMaxPeak;
            }

            // 平滑增益
            if (targetGain < gainReduction)
            {
                gainReduction = targetGain + (gainReduction - targetGain) * attackCoeff;
            }
            else
            {
                gainReduction = targetGain + (gainReduction - targetGain) * releaseCoeff;
            }

            // 输出延迟样本
            if (channels == 2)
            {
                buffer[bufferIndex] = lookaheadBuffer[readIndex] * gainReduction;
                buffer[bufferIndex + 1] = lookaheadBuffer[readIndex + 1] * gainReduction;
            }
            else
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    float delayedSample = lookaheadBuffer[readIndex + ch];
                    buffer[bufferIndex + ch] = delayedSample * gainReduction;
                }
            }

            writePos++;
            if (writePos >= lookaheadFrames) writePos = 0;

            readPos++;
            if (readPos >= lookaheadFrames) readPos = 0;
        }

        _gainReduction = gainReduction;
        _writePos = writePos;
        _readPos = readPos;
        _currentMaxPeak = currentMaxPeak;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DbToLinear(float db)
    {
        return MathF.Pow(10f, db / 20f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LinearToDb(float linear)
    {
        if (linear < 0.00001f) return -100.0f;
        return 20f * MathF.Log10(linear);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateCoefficients()
    {
        float sampleRate = WaveFormat.SampleRate;
        _attackCoeff = MathF.Exp(-1000f / (_attackTime * sampleRate));
        _releaseCoeff = MathF.Exp(-1000f / (_releaseTime * sampleRate));
    }

    public static MasterLimiterProvider UltraLowLatencyPreset(ISampleProvider sampleProvider)
    {
        return new MasterLimiterProvider(
            sampleProvider,
            thresholdDb: -2.0f,
            ceilingDb: -0.5f,
            attackMs: 0.5f,
            lookaheadMs: 1.5f,
            releaseMs: 40f
        );
    }

    public static MasterLimiterProvider GamePreset(ISampleProvider sampleProvider)
    {
        return new MasterLimiterProvider(
            sampleProvider,
            thresholdDb: -1.0f, // 稍微降低阈值，提前压制
            ceilingDb: -0.5f, // 降低天花板，防止Attack没来得及压住的瞬态溢出
            attackMs: 1.5f, // 从0.1加到1.5，消除物理切波的爆音
            lookaheadMs: 3f, // 3ms延迟，人耳不可察觉，但给了Attack反应时间
            releaseMs: 60f // 快速释放，适应高BPM密集的鼓点
        );
    }

    public static MasterLimiterProvider MusicPreset(ISampleProvider sampleProvider)
    {
        return new MasterLimiterProvider(
            sampleProvider,
            thresholdDb: -1.0f,
            ceilingDb: -0.1f,
            attackMs: 2f,
            lookaheadMs: 7.5f,
            releaseMs: 200f
        );
    }

    public static MasterLimiterProvider MasteringPreset(ISampleProvider sampleProvider)
    {
        return new MasterLimiterProvider(
            sampleProvider,
            thresholdDb: -0.5f,
            ceilingDb: -0.1f,
            attackMs: 1.0f,
            lookaheadMs: 5.0f,
            releaseMs: 300f
        );
    }
}