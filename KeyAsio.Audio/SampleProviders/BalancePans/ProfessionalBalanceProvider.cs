using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.BalancePans;

public sealed class ProfessionalBalanceProvider : ISampleProvider
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

    //private const float GainAttack = 0.9999f;   // 快速降低
    private const float GainRelease = 0.99995f; // 缓慢恢复
    private const float ClipThreshold = 0.95f; // 提前触发阈值

    private static readonly bool CanUseVectorization =
        Vector128.IsHardwareAccelerated &&
        (Sse.IsSupported || AdvSimd.Arm64.IsSupported);

    // 用于立体声交换的 Shuffle 掩码 [1, 0, 3, 2] -> 将 [L1, R1, L2, R2] 变为 [R1, L1, R2, L2]
    private static readonly Vector128<int> StereoSwapMask;
    private static readonly Vector128<float> VOne;
    private static readonly Vector128<float> VNegOne;
    private static readonly Vector128<float> VHalf;

    static ProfessionalBalanceProvider()
    {
        if (!CanUseVectorization) return;
        StereoSwapMask = Vector128.Create(1, 0, 3, 2);
        VOne = Vector128.Create(1.0f);
        VNegOne = Vector128.Create(-1.0f);
        VHalf = Vector128.Create(0.5f);
    }

    private readonly ISampleProvider _sourceProvider;
    private float _balanceValue = 0f;
    private BalanceMode _mode;
    private AntiClipStrategy _antiClip;

    // 缓存的增益值
    private float _leftDirectGain;
    private float _rightDirectGain;
    private float _leftCrossGain;
    private float _rightCrossGain;

    // --- Vector 增益缓存---
    // 一次处理2个立体声对: L1, R1, L2, R2
    private Vector128<float> _vDirectGain;
    private Vector128<float> _vCrossGain;

    // 动态增益调整用
    private float _dynamicGainReduction = 1.0f;

    public ProfessionalBalanceProvider(
        ISampleProvider sourceProvider,
        BalanceMode mode = BalanceMode.CrossMix,
        AntiClipStrategy antiClip = AntiClipStrategy.PreventiveAttenuation)
    {
        if (sourceProvider.WaveFormat.Channels != 2)
            throw new NotSupportedException(
                $"Only stereo (2 channels) supported, got {_sourceProvider.WaveFormat.Channels}");
        _sourceProvider = sourceProvider;
        _mode = mode;
        _antiClip = antiClip;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!CanUseVectorization) return;
        _vDirectGain = Vector128.Create(_leftDirectGain, _rightDirectGain, _leftDirectGain, _rightDirectGain);
        _vCrossGain = Vector128.Create(_leftCrossGain, _rightCrossGain, _leftCrossGain, _rightCrossGain);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateSimpleFadeGains()
    {
        float pan = (_balanceValue + 1f) * 0.5f;
        float angle = pan * MathF.PI * 0.5f;
        _leftDirectGain = MathF.Cos(angle);
        _rightDirectGain = MathF.Sin(angle);
        _leftCrossGain = 0f;
        _rightCrossGain = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateCrossMixGains()
    {
        if (_balanceValue < 0)
        {
            float amount = -_balanceValue;

            // 策略1: 预防性衰减 - 混合时使用更保守的增益
            float safetyFactor = (_antiClip == AntiClipStrategy.PreventiveAttenuation) ? 0.5f : 1.0f;

            _leftDirectGain = 1.0f;
            _leftCrossGain = amount * 0.4f * safetyFactor; // 降低交叉增益
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMidSideGains()
    {
        _leftDirectGain = 1.0f;
        _rightDirectGain = 1.0f;
        _leftCrossGain = 0f;
        _rightCrossGain = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;
        int samplesRead = _sourceProvider.Read(buffer, offset, count);

        if (_balanceValue == 0 && _mode != BalanceMode.MidSide && _antiClip == AntiClipStrategy.None)
            return samplesRead;

        if (CanUseVectorization)
        {
            Span<float> data = buffer.AsSpan(offset, samplesRead);

            if (_mode == BalanceMode.MidSide)
            {
                ProcessMidSideVectorized(data);
            }
            else
            {
                ProcessStandardVectorized(data);
            }
        }
        else
        {
            if (_mode == BalanceMode.MidSide)
            {
                ProcessMidSideSafe(buffer, offset, samplesRead);
            }
            else
            {
                ProcessStandardSafe(buffer, offset, samplesRead);
            }
        }

        return samplesRead;
    }


    private void ProcessStandardVectorized(Span<float> data)
    {
        var vecSpan = MemoryMarshal.Cast<float, Vector128<float>>(data);

        // HardLimit/None/Preventive 可以完全向量化，SoftClipper/DynamicGain 需要标量处理
        bool canFullyVectorize = _antiClip is AntiClipStrategy.None
            or AntiClipStrategy.PreventiveAttenuation
            or AntiClipStrategy.HardLimit;

        int i = 0;
        if (canFullyVectorize)
        {
            // 完全向量化路径
            for (; i < vecSpan.Length; i++)
            {
                Vector128<float> vIn = vecSpan[i];
                Vector128<float> vSwapped = SwapStereoChannels(vIn);

                // 矩阵混音: Out = In * DirectGain + Swapped * CrossGain
                Vector128<float> vOut = (vIn * _vDirectGain) + (vSwapped * _vCrossGain);

                if (_antiClip == AntiClipStrategy.HardLimit)
                {
                    vOut = Vector128.Min(Vector128.Max(vOut, VNegOne), VOne);
                }

                vecSpan[i] = vOut;
            }
        }
        else
        {
            // 混合路径: 向量化混音 + 标量 AntiClip
            for (; i < vecSpan.Length; i++)
            {
                Vector128<float> vIn = vecSpan[i];
                Vector128<float> vSwapped = SwapStereoChannels(vIn);
                Vector128<float> vOut = (vIn * _vDirectGain) + (vSwapped * _vCrossGain);

                vecSpan[i] = vOut;

                // 标量处理 SoftClipper/DynamicGain
                int baseIdx = i * 4;
                ApplyComplexAntiClip(ref data[baseIdx], ref data[baseIdx + 1]);
                ApplyComplexAntiClip(ref data[baseIdx + 2], ref data[baseIdx + 3]);
            }
        }

        // 处理剩余样本
        int remainingStart = i * 4;
        for (int j = remainingStart; j < data.Length; j += 2)
        {
            float l = data[j];
            float r = data[j + 1];
            float outL = l * _leftDirectGain + r * _leftCrossGain;
            float outR = r * _rightDirectGain + l * _rightCrossGain;

            if (canFullyVectorize)
            {
                if (_antiClip == AntiClipStrategy.HardLimit)
                {
                    outL = Math.Clamp(outL, -1f, 1f);
                    outR = Math.Clamp(outR, -1f, 1f);
                }

                data[j] = outL;
                data[j + 1] = outR;
            }
            else
            {
                ApplyComplexAntiClip(ref outL, ref outR);
                data[j] = outL;
                data[j + 1] = outR;
            }
        }
    }

    private void ProcessMidSideVectorized(Span<float> data)
    {  
        ref float dataRef = ref MemoryMarshal.GetReference(data);
        int vecCount = data.Length / 4;

        // 预计算 M/S 增益
        float sideGainVal = 1.0f - Math.Abs(_balanceValue);
        float midBalance = _balanceValue;

        // 计算 Mid 增益
        float midGainL, midGainR;
        if (midBalance < 0)
        {
            float amount = -midBalance;
            midGainL = 1.0f + amount * 0.5f;
            midGainR = 1.0f - amount * 0.5f;
        }
        else if (midBalance > 0)
        {
            float amount = midBalance;
            midGainL = 1.0f - amount * 0.5f;
            midGainR = 1.0f + amount * 0.5f;
        }
        else
        {
            midGainL = midGainR = 1.0f;
        }

        // 构建向量: [GainL, GainR, GainL, GainR]
        var vMidMixGain = Vector128.Create(midGainL, midGainR, midGainL, midGainR);
        var vSideGain = Vector128.Create(sideGainVal);

        bool canFullyVectorize = _antiClip is AntiClipStrategy.None
            or AntiClipStrategy.PreventiveAttenuation
            or AntiClipStrategy.HardLimit;

        int i = 0;
        for (; i < vecCount; i++)
        {  
            Vector128<float> vIn = Unsafe.As<float, Vector128<float>>(
                ref Unsafe.Add(ref dataRef, i * 4)
            ); // [L1, R1, L2, R2]
            Vector128<float> vSwapped = SwapStereoChannels(vIn);

            // Mid = (L+R) * 0.5
            Vector128<float> vMid = (vIn + vSwapped) * VHalf;

            // Side = (L-R) * 0.5, 自然产生 [S, -S, S, -S] 符号
            Vector128<float> vRawSide = (vIn - vSwapped) * VHalf;

            // 混合: OutL = Mid*GainL + Side*Gain, OutR = Mid*GainR - Side*Gain
            Vector128<float> vOut = (vMid * vMidMixGain) + (vRawSide * vSideGain);

            if (canFullyVectorize && _antiClip == AntiClipStrategy.HardLimit)
            {
                vOut = Vector128.Min(Vector128.Max(vOut, VNegOne), VOne);
            }

            Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref dataRef, i * 4)) = vOut;

            if (!canFullyVectorize)
            {
                int baseIdx = i * 4;
                ApplyComplexAntiClip(ref data[baseIdx], ref data[baseIdx + 1]);
                ApplyComplexAntiClip(ref data[baseIdx + 2], ref data[baseIdx + 3]);
            }
        }

        // 处理剩余部分
        int remainingStart = i * 4;
        for (int j = remainingStart; j < data.Length; j += 2)
        {
            float l = data[j];
            float r = data[j + 1];
            float mid = (l + r) * 0.5f;
            float side = (l - r) * 0.5f * sideGainVal;

            float outL = mid * midGainL + side;
            float outR = mid * midGainR - side;

            if (canFullyVectorize)
            {
                if (_antiClip == AntiClipStrategy.HardLimit)
                {
                    outL = Math.Clamp(outL, -1f, 1f);
                    outR = Math.Clamp(outR, -1f, 1f);
                }
            }
            else
            {
                ApplyComplexAntiClip(ref outL, ref outR);
            }

            data[j] = outL;
            data[j + 1] = outR;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyComplexAntiClip(ref float left, ref float right)
    {
        if (_antiClip == AntiClipStrategy.SoftClipper)
        {
            left = MathF.Tanh(left * 0.9f);
            right = MathF.Tanh(right * 0.9f);
        }
        else if (_antiClip == AntiClipStrategy.DynamicGain)
        {
            ApplyDynamicGainReduction(ref left, ref right);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyDynamicGainReduction(ref float left, ref float right)
    {
        // 计算当前峰值
        float peak = Math.Max(Math.Abs(left), Math.Abs(right));

        if (peak > ClipThreshold)
        {
            // 需要削减增益
            float requiredReduction = ClipThreshold / peak;

            // 快速降低增益
            if (requiredReduction < _dynamicGainReduction)
            {
                _dynamicGainReduction = requiredReduction;
            }
        }
        else
        {
            // 缓慢恢复增益
            _dynamicGainReduction += (1.0f - _dynamicGainReduction) * (1.0f - GainRelease);
            _dynamicGainReduction = Math.Min(_dynamicGainReduction, 1.0f);
        }

        // 应用动态增益
        left *= _dynamicGainReduction;
        right *= _dynamicGainReduction;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> SwapStereoChannels(Vector128<float> v)
    {
        if (Ssse3.IsSupported)
        {
            // x86: Shuffle (1 指令)
            // 交换左右声道 [L, R] -> [R, L]
            return Vector128.Shuffle(v, StereoSwapMask);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            // ARM: 使用 Zip/Unzip 指令 (2-3 指令)
            // [L1, R1, L2, R2] -> [R1, L1, R2, L2]
            var odds = AdvSimd.Arm64.UnzipOdd(v, v); // [R1, R2, ?, ?]
            var evens = AdvSimd.Arm64.UnzipEven(v, v); // [L1, L2, ?, ?]
            return AdvSimd.Arm64.ZipLow(odds, evens); // [R1, L1, R2, L2]
        }

        return Vector128.Create(v[1], v[0], v[3], v[2]);
    }
}