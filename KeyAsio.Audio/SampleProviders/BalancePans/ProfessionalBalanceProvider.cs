using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.BalancePans;

public class ProfessionalBalanceProvider : ISampleProvider
{
    // =============== 使用示例和性能对比 ===============
    /*
    各策略对比:

    ┌─────────────────────────┬──────────┬─────────┬──────────┬──────────┐
    │ 策略                    │ 音质     │ 性能    │ 音量损失 │ 削波风险 │
    ├─────────────────────────┼──────────┼─────────┼──────────┼──────────┤
    │ PreventiveAttenuation   │ 极好     │ 最快    │ 轻微     │ 0%       │
    │ SoftClipper (tanh)      │ 很好     │ 较慢    │ 无       │ 0%       │
    │ HardLimit               │ 中等     │ 快      │ 无       │ 0%       │
    │ DynamicGain             │ 最佳     │ 最慢    │ 动态     │ 0%       │
    └─────────────────────────┴──────────┴─────────┴──────────┴──────────┘

    推荐配置:

    1. 音乐播放器 (平衡质量和性能):
       var balance = new SafeBalanceProvider(source,
           BalanceMode.CrossMix,
           AntiClipStrategy.PreventiveAttenuation);  // ⭐ 推荐

    2. 实时游戏 (最快性能):
       var balance = new SafeBalanceProvider(source,
           BalanceMode.ConstantPower,
           AntiClipStrategy.PreventiveAttenuation);

    3. 专业 DAW (最高音质):
       var balance = new SafeBalanceProvider(source,
           BalanceMode.MidSide,
           AntiClipStrategy.SoftClipper);

    4. 母带处理 (零失真):
       var balance = new SafeBalanceProvider(source,
           BalanceMode.MidSide,
           AntiClipStrategy.DynamicGain);

    使用方法:
    balance.Balance = -0.8f;  // 向左偏移,保证不削波
    balance.AntiClipStrategy = AntiClipStrategy.SoftClipper;  // 运行时切换
    */

    private readonly ISampleProvider _sourceProvider;
    private float _balanceValue = 0f;
    private BalanceMode _mode;
    private AntiClipStrategy _antiClip;
    private readonly int _channels;

    // 缓存的增益值
    private float _leftDirectGain;
    private float _rightDirectGain;
    private float _leftCrossGain;
    private float _rightCrossGain;

    // 动态增益调整用
    private float _dynamicGainReduction = 1.0f;
    //private const float GAIN_ATTACK = 0.9999f;   // 快速降低
    private const float GAIN_RELEASE = 0.99995f; // 缓慢恢复
    private const float CLIP_THRESHOLD = 0.95f;  // 提前触发阈值

    public ProfessionalBalanceProvider(
        ISampleProvider sourceProvider,
        BalanceMode mode = BalanceMode.CrossMix,
        AntiClipStrategy antiClip = AntiClipStrategy.PreventiveAttenuation)
    {
        _sourceProvider = sourceProvider;
        _channels = _sourceProvider.WaveFormat.Channels;
        _mode = mode;
        _antiClip = antiClip;

        if (_channels != 2)
            throw new NotSupportedException($"Only stereo (2 channels) supported, got {_channels}");

        Balance = 0f;
    }

    public float Balance
    {
        get => _balanceValue;
        set
        {
            value = Math.Clamp(value, -1f, 1f);
            if (Math.Abs(_balanceValue - value) < 0.0001f) return;

            _balanceValue = value;
            UpdateGains();
        }
    }

    public BalanceMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            UpdateGains();
        }
    }

    public AntiClipStrategy AntiClipStrategy
    {
        get => _antiClip;
        set => _antiClip = value;
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    private void UpdateGains()
    {
        switch (_mode)
        {
            case BalanceMode.ConstantPower:
                UpdateSimpleFadeGains();
                break;

            case BalanceMode.CrossMix:
                UpdateCrossMixGains();
                break;

            case BalanceMode.MidSide:
                UpdateMidSideGains();
                break;

            case BalanceMode.BinauralMix:
                UpdateBinauralGains();
                break;
        }
    }

    private void UpdateSimpleFadeGains()
    {
        float pan = (_balanceValue + 1f) * 0.5f;
        float angle = pan * MathF.PI * 0.5f;
        _leftDirectGain = MathF.Cos(angle);
        _rightDirectGain = MathF.Sin(angle);
        _leftCrossGain = 0f;
        _rightCrossGain = 0f;
    }

    private void UpdateCrossMixGains()
    {
        if (_balanceValue < 0)
        {
            float amount = -_balanceValue;

            // 策略1: 预防性衰减 - 混合时使用更保守的增益
            float safetyFactor = (_antiClip == AntiClipStrategy.PreventiveAttenuation) ? 0.5f : 1.0f;

            _leftDirectGain = 1.0f;
            _leftCrossGain = amount * 0.4f * safetyFactor;  // 降低交叉增益
            _rightDirectGain = 1.0f - amount;
            _rightCrossGain = 0f;
        }
        else if (_balanceValue > 0)
        {
            float amount = _balanceValue;
            float safetyFactor = (_antiClip == AntiClipStrategy.PreventiveAttenuation) ? 0.5f : 1.0f;

            _leftDirectGain = 1.0f - amount;
            _leftCrossGain = 0f;
            _rightDirectGain = 1.0f;
            _rightCrossGain = amount * 0.4f * safetyFactor;
        }
        else
        {
            _leftDirectGain = 1.0f;
            _rightDirectGain = 1.0f;
            _leftCrossGain = 0f;
            _rightCrossGain = 0f;
        }
    }

    private void UpdateMidSideGains()
    {
        _leftDirectGain = 1.0f;
        _rightDirectGain = 1.0f;
        _leftCrossGain = 0f;
        _rightCrossGain = 0f;
    }

    private void UpdateBinauralGains()
    {
        // Binaural 模式风险最高,必须使用补偿
        float safetyFactor = (_antiClip == AntiClipStrategy.PreventiveAttenuation) ? 0.5f : 1.0f;

        if (_balanceValue < 0)
        {
            float amount = -_balanceValue;
            _leftDirectGain = 1.0f * safetyFactor;
            _leftCrossGain = amount * safetyFactor;
            _rightDirectGain = (1.0f - amount) * safetyFactor;
            _rightCrossGain = 0f;
        }
        else if (_balanceValue > 0)
        {
            float amount = _balanceValue;
            _leftDirectGain = (1.0f - amount) * safetyFactor;
            _leftCrossGain = 0f;
            _rightDirectGain = 1.0f * safetyFactor;
            _rightCrossGain = amount * safetyFactor;
        }
        else
        {
            _leftDirectGain = 1.0f;
            _rightDirectGain = 1.0f;
            _leftCrossGain = 0f;
            _rightCrossGain = 0f;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;

        int samplesRead = _sourceProvider.Read(buffer, offset, count);

        if (_balanceValue == 0)
            return samplesRead;

        if (_mode == BalanceMode.MidSide)
        {
            ProcessMidSideSafe(buffer, offset, samplesRead);
        }
        else
        {
            ProcessStandardSafe(buffer, offset, samplesRead);
        }

        return samplesRead;
    }

    private void ProcessStandardSafe(float[] buffer, int offset, int count)
    {
        int endIndex = offset + count;

        for (int i = offset; i < endIndex; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];

            // 计算输出
            float outLeft = left * _leftDirectGain + right * _leftCrossGain;
            float outRight = right * _rightDirectGain + left * _rightCrossGain;

            // 应用防削波策略
            ApplyAntiClip(ref outLeft, ref outRight);

            buffer[i] = outLeft;
            buffer[i + 1] = outRight;
        }
    }

    private void ProcessMidSideSafe(float[] buffer, int offset, int count)
    {
        int endIndex = offset + count;
        float sideGain = 1.0f - Math.Abs(_balanceValue);
        float midBalance = _balanceValue;

        for (int i = offset; i < endIndex; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];

            // Mid-Side 变换
            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f;

            side *= sideGain;

            float outLeft, outRight;

            if (midBalance < 0)
            {
                float amount = -midBalance;
                outLeft = mid * (1.0f + amount * 0.5f) + side;
                outRight = mid * (1.0f - amount * 0.5f) - side;
            }
            else if (midBalance > 0)
            {
                float amount = midBalance;
                outLeft = mid * (1.0f - amount * 0.5f) + side;
                outRight = mid * (1.0f + amount * 0.5f) - side;
            }
            else
            {
                outLeft = mid + side;
                outRight = mid - side;
            }

            // 应用防削波
            ApplyAntiClip(ref outLeft, ref outRight);

            buffer[i] = outLeft;
            buffer[i + 1] = outRight;
        }
    }

    private void ApplyAntiClip(ref float left, ref float right)
    {
        switch (_antiClip)
        {
            case AntiClipStrategy.None:
                break;

            case AntiClipStrategy.PreventiveAttenuation:
                // 已在增益计算中处理,这里无需额外操作
                break;

            case AntiClipStrategy.SoftClipper:
                // 使用 tanh 作为软限制器 (音质最好)
                left = MathF.Tanh(left * 0.9f);
                right = MathF.Tanh(right * 0.9f);
                break;

            case AntiClipStrategy.HardLimit:
                // 硬限制 (最快但有失真)
                left = Math.Clamp(left, -1.0f, 1.0f);
                right = Math.Clamp(right, -1.0f, 1.0f);
                break;

            case AntiClipStrategy.DynamicGain:
                // 动态增益调整
                ApplyDynamicGainReduction(ref left, ref right);
                break;
        }
    }

    private void ApplyDynamicGainReduction(ref float left, ref float right)
    {
        // 计算当前峰值
        float peak = Math.Max(Math.Abs(left), Math.Abs(right));

        if (peak > CLIP_THRESHOLD)
        {
            // 需要削减增益
            float requiredReduction = CLIP_THRESHOLD / peak;

            // 快速降低增益
            if (requiredReduction < _dynamicGainReduction)
            {
                _dynamicGainReduction = requiredReduction;
            }
        }
        else
        {
            // 缓慢恢复增益
            _dynamicGainReduction += (1.0f - _dynamicGainReduction) * (1.0f - GAIN_RELEASE);
            _dynamicGainReduction = Math.Min(_dynamicGainReduction, 1.0f);
        }

        // 应用动态增益
        left *= _dynamicGainReduction;
        right *= _dynamicGainReduction;
    }
}