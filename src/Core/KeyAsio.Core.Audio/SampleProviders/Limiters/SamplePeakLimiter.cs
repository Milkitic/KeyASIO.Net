using System.Diagnostics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// Provides a zero-latency peak limiter for mastering.
/// </summary>
/// <remarks>
/// This provider implements <see cref="ISampleProvider"/> and tracks recent peak
/// levels over a configurable window. When a peak exceeds the threshold, it applies
/// gain reduction smoothly based on attack and release times. Because it has no
/// lookahead delay, the configured ceiling is a gain target rather than a guaranteed
/// brick-wall output ceiling.
/// </remarks>
public sealed class SamplePeakLimiter : LimiterBase
{
    private readonly int _channels;
    private readonly int _peakWindowFrames;

    private readonly float[] _peakBuffer; // 存储每帧的峰值

    private float _thresholdLinear;
    private float _ceilingLinear;
    private float _attackTime; 
    private float _releaseTime;

    private float _currentGain = 1.0f;
    private float _attackCoeff;
    private float _releaseCoeff;

    private int _writePos;
    private float _currentMaxPeak; // 当前窗口最大峰值

    /// <summary>
    /// Initializes a new instance of the <see cref ="SamplePeakLimiter"/> class.
    /// </summary>
    /// <param name="source">The source sample provider to apply the limiter to.</param>
    /// <param name="thresholdDb">The threshold in decibels (dB) at which limiting starts. Default is -0.5 dB.</param>
    /// <param name="targetLevelDb">The target output ceiling in decibels (dB). Default is -0.1 dB.</param>
    /// <param name="attackMs">The attack time in milliseconds (ms) for the gain reduction. Default is 0.1 ms.</param>
    /// <param name="releaseMs">The release time in milliseconds (ms) for the gain reduction. Default is 50 ms.</param>
    /// <param name="peakWindowMs">The recent-peak window in milliseconds (ms). Default is 2 ms.</param>
    public SamplePeakLimiter(
        ISampleProvider source,
        float thresholdDb = -0.5f,
        float targetLevelDb = -0.1f,
        float attackMs = 0.1f,
        float releaseMs = 50f,
        float peakWindowMs = 2f) : base(source)
    {
        _channels = source.WaveFormat.Channels;
        _peakWindowFrames = Math.Max(1, (int)(source.WaveFormat.SampleRate * peakWindowMs / 1000f));
        _peakBuffer = new float[_peakWindowFrames];

        ThresholdDb = thresholdDb;
        TargetLevelDb = targetLevelDb;
        AttackTime = attackMs;
        ReleaseTime = releaseMs;

        _writePos = 0;
        _currentMaxPeak = 0f;
    }

    /// <summary>
    /// Gets the current amount of gain reduction being applied, as a linear scalar.
    /// </summary>
    /// <value>
    /// A value of 0.0 indicates no gain reduction. A value of 0.1 indicates that the
    /// signal is being attenuated by 10% (i.e., multiplied by 0.9).
    /// </value>
    public float CurrentGainReduction => 1.0f - _currentGain;

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
    /// The target output level (dB) used to calculate gain reduction.
    /// </value>
    public float TargetLevelDb
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

    protected override void Process(float[] buffer, int offset, int count)
    {
        int channels = _channels;
        int frameCount = count / _channels;

        float gainReduction = _currentGain;
        int writePos = _writePos;
        float currentMaxPeak = _currentMaxPeak;

        float thresholdLinear = _thresholdLinear;
        float ceilingLinear = _ceilingLinear;
        float targetPeakLinear = Math.Min(thresholdLinear, ceilingLinear);

        float attackCoeff = _attackCoeff;
        float releaseCoeff = _releaseCoeff;
        int peakWindowFrames = _peakWindowFrames;

        Span<float> peakBuffer = _peakBuffer.AsSpan();

        for (int frame = 0; frame < frameCount; frame++)
        {
            int bufferIndex = offset + (frame * channels);

            // 计算输入帧的峰值
            float inputPeak = 0f;
            if (channels == 2)
            {
                float s0 = buffer[bufferIndex];
                float s1 = buffer[bufferIndex + 1];

                inputPeak = Math.Max(Math.Abs(s0), Math.Abs(s1));
            }
            else
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = buffer[bufferIndex + ch];
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
                targetGain = targetPeakLinear / currentMaxPeak;
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

            // 零延迟输出当前帧
            for (int ch = 0; ch < channels; ch++)
            {
                buffer[bufferIndex + ch] *= gainReduction;
            }

            writePos++;
            if (writePos >= peakWindowFrames) writePos = 0;
        }

        _currentGain = gainReduction;
        _writePos = writePos;
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

    public static SamplePeakLimiter FastPreset(ISampleProvider sampleProvider)
    {
        return new SamplePeakLimiter(
            sampleProvider,
            thresholdDb: -2.0f,
            targetLevelDb: -0.5f,
            attackMs: 0.5f,
            peakWindowMs: 1.5f,
            releaseMs: 40f
        );
    }

    public static SamplePeakLimiter GamePreset(ISampleProvider sampleProvider)
    {
        return new SamplePeakLimiter(
            sampleProvider,
            thresholdDb: -1.0f, // 稍微降低阈值，提前压制
            targetLevelDb: -0.5f, // 降低天花板，防止Attack没来得及压住的瞬态溢出
            attackMs: 1.5f, // 从0.1加到1.5，消除物理切波的爆音
            peakWindowMs: 3f, // 在短窗口内保持峰值，避免增益过快恢复
            releaseMs: 60f // 快速释放，适应高BPM密集的鼓点
        );
    }

    public static SamplePeakLimiter MusicPreset(ISampleProvider sampleProvider)
    {
        return new SamplePeakLimiter(
            sampleProvider,
            thresholdDb: -1.0f,
            targetLevelDb: -0.1f,
            attackMs: 2f,
            peakWindowMs: 7.5f,
            releaseMs: 200f
        );
    }

    public static SamplePeakLimiter SmoothPreset(ISampleProvider sampleProvider)
    {
        return new SamplePeakLimiter(
            sampleProvider,
            thresholdDb: -0.5f,
            targetLevelDb: -0.1f,
            attackMs: 1.0f,
            peakWindowMs: 5.0f,
            releaseMs: 300f
        );
    }
}
