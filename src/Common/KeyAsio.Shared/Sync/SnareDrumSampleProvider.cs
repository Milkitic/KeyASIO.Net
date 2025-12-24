using KeyAsio.Core.Audio.SampleProviders;
using NAudio.Wave;

namespace KeyAsio.Shared.Sync;

/// <summary>
/// 军鼓生成器
/// </summary>
public class SnareDrumSampleProvider : IRecyclableProvider
{
    private readonly WaveFormat _waveFormat;
    private readonly Random _random;

    // 状态变量
    private long _sampleCount;
    private float _snapGain;
    private float _snareGain;
    private float _phase;
    private long _maxDurationSamples;

    // 物理参数配置 (时间单位：秒)
    /// <summary>
    /// 军鼓基频 (Hz)
    /// </summary>
    public float FundamentalFrequency { get; set; } = 180f;

    /// <summary>
    /// 频率滑落范围 (Hz)
    /// </summary>
    public float FrequencySweepRange { get; set; } = 150f;

    /// <summary>
    /// 鼓面打击衰减时长 (秒)
    /// </summary>
    public float SnapDecayDuration { get; set; } = 0.08f;

    /// <summary>
    /// 响弦噪声衰减时长 (秒)
    /// </summary>
    public float SnareDecayDuration { get; set; } = 0.1f;

    private float _totalDuration = 1.0f;

    /// <summary>
    /// 总时长 (秒)
    /// </summary>
    public float TotalDuration
    {
        get => _totalDuration;
        set
        {
            _totalDuration = value;
            UpdateMaxDurationSamples();
        }
    }

    /// <summary>
    /// 频率滑落时间常数 (秒)
    /// </summary>
    public float PitchDecayTimeConstant { get; set; } = 0.013605442f;

    /// <summary>
    /// 初始打击增益 (0-1)
    /// </summary>
    public float InitialSnapGain { get; set; } = 0.9f;

    /// <summary>
    /// 初始响弦增益 (0-1)
    /// </summary>
    public float InitialSnareGain { get; set; } = 0.6f;

    /// <summary>
    /// 冲击瞬态时长 (秒)
    /// </summary>
    public float ImpactDuration { get; set; } = 0.0035f;

    /// <summary>
    /// 冲击瞬态强度 (0-1)
    /// </summary>
    public float ImpactLevel { get; set; } = 0.5f;

    /// <summary>
    /// 鼓面打击混合比例 (0-1)
    /// </summary>
    public float SnapMixLevel { get; set; } = 0.5f;

    /// <summary>
    /// 响弦噪声混合比例 (0-1)
    /// </summary>
    public float SnareMixLevel { get; set; } = 0.4f;

    public WaveFormat WaveFormat => _waveFormat;
    public ISampleProvider? ResetAndGetSource()
    {
        Reset();
        return null;
    }

    /// <summary>
    /// 创建一个新的军鼓击打实例
    /// </summary>
    /// <param name="targetFormat">混合器的目标格式（支持任意采样率和声道数）</param>
    public SnareDrumSampleProvider(WaveFormat targetFormat)
    {
        _waveFormat = targetFormat;
        _random = Random.Shared;

        // 初始化增益
        _snapGain = InitialSnapGain;
        _snareGain = InitialSnareGain;
        _phase = 0;
        _sampleCount = 0;

        // 根据采样率计算总采样数
        UpdateMaxDurationSamples();
    }

    private void UpdateMaxDurationSamples()
    {
        if (_waveFormat != null)
        {
            _maxDurationSamples = (long)(TotalDuration * _waveFormat.SampleRate);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = 0;
        int channels = _waveFormat.Channels;
        int sampleRate = _waveFormat.SampleRate;

        // 计算每帧处理多少个采样点 (Buffer是交错的：L, R, L, R...)
        // 我们需要填充的 "帧" 数 = count / channels
        int samplesToFill = count / channels;

        // --- 预计算和缓存 (性能优化) ---
        // 缓存属性到局部变量，避免循环中重复访问属性 getter
        float fundamentalFreq = FundamentalFrequency;
        float freqSweepRange = FrequencySweepRange;
        float pitchDecayTimeConstant = PitchDecayTimeConstant;
        float snapMixLevel = SnapMixLevel;
        float snareMixLevel = SnareMixLevel;
        float impactLevel = ImpactLevel;
        
        // 预计算衰减因子
        // MathF.Exp(-1.0f / (sampleRate * PitchDecayTimeConstant))
        float freqDecayFactor = MathF.Exp(-1.0f / (sampleRate * pitchDecayTimeConstant));
        
        // 预计算当前频率包络值 (避免循环内调用 Exp)
        // 初始值基于当前 _sampleCount
        float currentFreqEnv = MathF.Exp(-((float)_sampleCount / sampleRate) / pitchDecayTimeConstant);

        // 预计算 Snap 和 Snare 的衰减因子
        // 原公式: Gain *= (1.0f - 1.0f / (sampleRate * Duration))
        float snapDecayFactor = 1.0f - 1.0f / (sampleRate * SnapDecayDuration);
        float snareDecayFactor = 1.0f - 1.0f / (sampleRate * SnareDecayDuration);

        // 预计算 Impact 持续的采样数，避免循环内浮点除法和比较
        long impactDurationSamples = (long)(ImpactDuration * sampleRate);

        // 预计算相位增量常数
        float twoPi = 2f * MathF.PI;
        float phaseIncrementBase = twoPi / sampleRate;

        // 缓存 Random 实例
        Random random = _random;

        for (int i = 0; i < samplesToFill; i++)
        {
            if (_sampleCount >= _maxDurationSamples)
            {
                break;
            }

            // --- 合成核心逻辑 (基于单声道计算) ---

            // 2. 合成 "Snap" (正弦波 + 快速频率掉落)
            // 使用累乘更新频率包络
            float freq = fundamentalFreq + (freqSweepRange * currentFreqEnv); 
            currentFreqEnv *= freqDecayFactor;

            _phase += phaseIncrementBase * freq;
            if (_phase > twoPi) _phase -= twoPi;

            float snap = MathF.Sin(_phase) * _snapGain;

            // 3. 合成 "Snare" (白噪声)
            // 使用 NextSingle 替代 NextDouble 以提升性能
            float noise = (random.NextSingle() * 2f - 1f) * _snareGain;

            // 4. "Impact" 瞬态
            float impact = 0f;
            if (_sampleCount < impactDurationSamples)
            {
                impact = (random.NextSingle() * 2f - 1f) * impactLevel;
            }

            // 5. 混合
            float sampleValue = (snap * snapMixLevel) + (noise * snareMixLevel) + impact;

            // 硬限制器
            if (sampleValue > 1.0f) sampleValue = 1.0f;
            else if (sampleValue < -1.0f) sampleValue = -1.0f;

            // --- 声道处理 ---
            for (int ch = 0; ch < channels; ch++)
            {
                buffer[offset + samplesRead + ch] = sampleValue;
            }

            samplesRead += channels;

            // 6. 包络衰减
            _snapGain *= snapDecayFactor;
            _snareGain *= snareDecayFactor;

            _sampleCount++;
        }

        if (samplesRead < count)
        {
            Array.Clear(buffer, offset + samplesRead, count - samplesRead);
        }

        return samplesRead;
    }

    private void Reset()
    {
        _sampleCount = 0;
        _snapGain = InitialSnapGain;
        _snareGain = InitialSnareGain;
        _phase = 0;
    }
}