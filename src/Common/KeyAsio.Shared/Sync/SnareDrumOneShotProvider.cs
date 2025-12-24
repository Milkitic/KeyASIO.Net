using NAudio.Wave;

namespace KeyAsio.Shared.Sync;

/// <summary>
/// 军鼓生成器
/// </summary>
public class SnareDrumOneShotProvider : ISampleProvider
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

    /// <summary>
    /// 创建一个新的军鼓击打实例
    /// </summary>
    /// <param name="targetFormat">混合器的目标格式（支持任意采样率和声道数）</param>
    public SnareDrumOneShotProvider(WaveFormat targetFormat)
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

        for (int i = 0; i < samplesToFill; i++)
        {
            if (_sampleCount >= _maxDurationSamples)
            {
                break;
            }

            // --- 合成核心逻辑 (基于单声道计算) ---

            // 当前时间的秒数
            float timeSeconds = (float)_sampleCount / sampleRate;

            // 2. 合成 "Snap" (正弦波 + 快速频率掉落)
            // 增加频率冲击感，从更高频率快速滑落
            // 使用基于时间的计算，适应不同采样率
            float freqEnv = MathF.Exp(-timeSeconds / PitchDecayTimeConstant);
            float freq = FundamentalFrequency + (FrequencySweepRange * freqEnv); // 初始频率 + SweepRange

            _phase += 2f * MathF.PI * freq / sampleRate;
            // 防止相位溢出 (虽然 float 范围很大，但好习惯)
            if (_phase > MathF.PI * 2) _phase -= MathF.PI * 2;

            float snap = MathF.Sin(_phase) * _snapGain;

            // 3. 合成 "Snare" (白噪声)
            float noise = ((float)_random.NextDouble() * 2f - 1f) * _snareGain;

            // 4. "Impact" 瞬态 (最初的几毫秒增加额外冲击)
            float impact = 0f;
            if (timeSeconds < ImpactDuration)
            {
                impact = ((float)_random.NextDouble() * 2f - 1f) * ImpactLevel;
            }

            // 5. 混合 (调整混合比例以防止削波)
            // Snap 提供低频力度，Noise 提供高频色彩，Impact 提供触头
            float sampleValue = (snap * SnapMixLevel) + (noise * SnareMixLevel) + impact;

            // 简单的硬限制器防止爆音
            if (sampleValue > 1.0f) sampleValue = 1.0f;
            else if (sampleValue < -1.0f) sampleValue = -1.0f;

            // --- 声道处理 ---

            // 将单声道信号复制到所有声道 (例如立体声：左=sample, 右=sample)
            for (int ch = 0; ch < channels; ch++)
            {
                buffer[offset + samplesRead + ch] = sampleValue;
            }

            samplesRead += channels;

            // 6. 包络衰减 (基于时间的指数衰减公式: Gain = Gain * e^(-1/sampleRate * decay))
            // 这里为了性能，用原本的乘法近似：(1 - 1 / (Rate * Duration))
            _snapGain *= (1.0f - 1.0f / (sampleRate * SnapDecayDuration));
            _snareGain *= (1.0f - 1.0f / (sampleRate * SnareDecayDuration));

            _sampleCount++;
        }

        // 如果没有填满 Buffer (因为声音结束了)，将剩余部分填 0
        // 这对于某些不仅依赖返回值还依赖 buffer 内容的播放器很重要
        if (samplesRead < count)
        {
            Array.Clear(buffer, offset + samplesRead, count - samplesRead);
        }

        return samplesRead;
    }
}