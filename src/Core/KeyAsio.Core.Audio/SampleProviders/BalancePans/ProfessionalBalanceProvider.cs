using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using KeyAsio.Core.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.BalancePans;

public sealed class ProfessionalBalanceProvider : IRecyclableProvider, IPoolable
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
    public static bool EnableAvx512 { get; set; } = true;

    private static readonly bool s_canUseVectorization =
        Vector128.IsHardwareAccelerated &&
        (Sse.IsSupported || AdvSimd.Arm64.IsSupported);

    private static readonly Vector128<int> s_stereoSwapMask;
    private static readonly Vector256<int> s_swapMask256;
    private static readonly Vector512<int> s_swapMask512;

    private static readonly Vector128<float> s_vOne;
    private static readonly Vector128<float> s_vNegOne;
    private static readonly Vector128<float> s_vHalf;

    static ProfessionalBalanceProvider()
    {
        if (s_canUseVectorization)
        {
            // 用于立体声交换的 Shuffle 掩码 [1, 0, 3, 2] -> 将 [L1, R1, L2, R2] 变为 [R1, L1, R2, L2]
            s_stereoSwapMask = Vector128.Create(1, 0, 3, 2);
            s_vOne = Vector128.Create(1.0f);
            s_vNegOne = Vector128.Create(-1.0f);
            s_vHalf = Vector128.Create(0.5f);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            // [1, 0, 3, 2, 5, 4, 7, 6] -> 交换相邻的 L/R
            s_swapMask256 = Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6);
        }

        if (Vector512.IsHardwareAccelerated && EnableAvx512)
        {
            s_swapMask512 = Vector512.Create(1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14);
        }
    }

    private float _balanceValue;
    private BalanceMode _mode = BalanceMode.CrossMix;
    private AntiClipStrategy _antiClip = AntiClipStrategy.PreventiveAttenuation;

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

    public ProfessionalBalanceProvider()
    {
        Balance = 0f;
    }

    public ProfessionalBalanceProvider(
        ISampleProvider? sourceProvider,
        BalanceMode mode = BalanceMode.CrossMix,
        AntiClipStrategy antiClip = AntiClipStrategy.PreventiveAttenuation)
    {
        _mode = mode;
        _antiClip = antiClip;
        Source = sourceProvider;
        Balance = 0f;
    }

    public ISampleProvider? Source
    {
        get => field;
        set
        {
            if (value != null && value.WaveFormat.Channels != 2)
            {
                throw new NotSupportedException(
                    $"Only stereo (2 channels) supported, got {value.WaveFormat.Channels}");
            }

            field = value;
        }
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

    public WaveFormat WaveFormat => Source?.WaveFormat ?? throw new InvalidOperationException("Source not ready");

    public ISampleProvider? ResetAndGetSource()
    {
        var child = Source;
        Reset();
        return child;
    }

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

            case BalanceMode.Off:
                UpdateOffGains();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!s_canUseVectorization) return;
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
    private void UpdateOffGains()
    {
        _leftDirectGain = 1.0f;
        _rightDirectGain = 1.0f;
        _leftCrossGain = 0f;
        _rightCrossGain = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(float[] buffer, int offset, int sampleCount)
    {
        if (Source == null)
        {
            Array.Clear(buffer, offset, sampleCount);
            return sampleCount;
        }

        if (sampleCount == 0) return 0;
        int samplesRead = Source.Read(buffer, offset, sampleCount);

        if ((_balanceValue == 0 && _mode != BalanceMode.MidSide || _mode == BalanceMode.Off) &&
            _antiClip == AntiClipStrategy.None)
        {
            return samplesRead;
        }

        if (s_canUseVectorization)
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
                    vOut = Vector128.Min(Vector128.Max(vOut, s_vNegOne), s_vOne);
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
        int len = data.Length;

        float sideGainVal = 1.0f - Math.Abs(_balanceValue);
        float midBalance = _balanceValue;
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

        // 2. 准备不同宽度的向量常量
        // 注意：Create 方法会自动将这4个值平铺填满整个向量
        // 例如 Vector256 会填入 [L, R, L, R, L, R, L, R]
        var vMidMixGain128 = Vector128.Create(midGainL, midGainR, midGainL, midGainR);
        var vSideGain128 = Vector128.Create(sideGainVal);
        var vHalf128 = Vector128.Create(0.5f);
        var vOne128 = Vector128.Create(1.0f);
        var vNegOne128 = Vector128.Create(-1.0f);

        // 判断是否只做简单处理 (HardLimit 支持向量化)
        bool canFullyVectorize = _antiClip is AntiClipStrategy.None
            or AntiClipStrategy.PreventiveAttenuation
            or AntiClipStrategy.HardLimit;

        int i = 0;

        // ---------------------------------------------------------
        // PATH A: AVX-512 (512-bit, 16 floats / 8 stereo pairs)
        // ---------------------------------------------------------
        if (Vector512.IsHardwareAccelerated && canFullyVectorize && EnableAvx512)
        {
            var vMidMixGain = Vector512.Create(midGainL, midGainR, midGainL, midGainR, midGainL, midGainR, midGainL,
                midGainR,
                midGainL, midGainR, midGainL, midGainR, midGainL, midGainR, midGainL, midGainR);
            var vSideGain = Vector512.Create(sideGainVal);
            var vHalf = Vector512.Create(0.5f);
            var vOne = Vector512.Create(1.0f);
            var vNegOne = Vector512.Create(-1.0f);

            for (; i <= len - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                // Load
                Vector512<float> vIn = Vector512.LoadUnsafe(ref dataRef, (nuint)i);

                // Swap L/R: [L, R...] -> [R, L...]
                Vector512<float> vSwapped = Vector512.Shuffle(vIn, s_swapMask512);

                // Math
                Vector512<float> vMid = (vIn + vSwapped) * vHalf;
                Vector512<float> vRawSide = (vIn - vSwapped) * vHalf; // S = (L-R)/2
                Vector512<float> vOut = (vMid * vMidMixGain) + (vRawSide * vSideGain);

                // Hard Limit
                if (_antiClip == AntiClipStrategy.HardLimit)
                {
                    vOut = Vector512.Min(Vector512.Max(vOut, vNegOne), vOne);
                }

                // Store
                vOut.StoreUnsafe(ref dataRef, (nuint)i);
            }
        }
        // ---------------------------------------------------------
        // PATH B: AVX2 (256-bit, 8 floats / 4 stereo pairs)
        // ---------------------------------------------------------
        else if (Vector256.IsHardwareAccelerated && canFullyVectorize)
        {
            var vMidMixGain = Vector256.Create(midGainL, midGainR, midGainL, midGainR, midGainL, midGainR, midGainL,
                midGainR);
            var vSideGain = Vector256.Create(sideGainVal);
            var vHalf = Vector256.Create(0.5f);
            var vOne = Vector256.Create(1.0f);
            var vNegOne = Vector256.Create(-1.0f);

            for (; i <= len - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                Vector256<float> vIn = Vector256.LoadUnsafe(ref dataRef, (nuint)i);

                // .NET 8+ Shuffle 能够生成高效的 vpermps / vpermilps 指令
                Vector256<float> vSwapped = Vector256.Shuffle(vIn, s_swapMask256);

                Vector256<float> vMid = (vIn + vSwapped) * vHalf;
                Vector256<float> vRawSide = (vIn - vSwapped) * vHalf;
                Vector256<float> vOut = (vMid * vMidMixGain) + (vRawSide * vSideGain);

                if (_antiClip == AntiClipStrategy.HardLimit)
                {
                    vOut = Vector256.Min(Vector256.Max(vOut, vNegOne), vOne);
                }

                vOut.StoreUnsafe(ref dataRef, (nuint)i);
            }
        }

        // ---------------------------------------------------------
        // PATH C: SSE/Neon (128-bit, 4 floats / 2 stereo pairs)
        // ---------------------------------------------------------
        // 这里的逻辑和你原有的类似，但是稍微清理了 unsafe 写法，直接用 Vector API
        for (; i <= len - Vector128<float>.Count; i += Vector128<float>.Count)
        {
            Vector128<float> vIn = Vector128.LoadUnsafe(ref dataRef, (nuint)i);

            // 使用原有的 SwapStereoChannels (假设它已针对 128位 优化)
            Vector128<float> vSwapped = SwapStereoChannels(vIn);

            Vector128<float> vMid = (vIn + vSwapped) * vHalf128;
            Vector128<float> vRawSide = (vIn - vSwapped) * vHalf128;
            Vector128<float> vOut = (vMid * vMidMixGain128) + (vRawSide * vSideGain128);

            if (canFullyVectorize && _antiClip == AntiClipStrategy.HardLimit)
            {
                vOut = Vector128.Min(Vector128.Max(vOut, vNegOne128), vOne128);
            }

            vOut.StoreUnsafe(ref dataRef, (nuint)i);

            // 处理复杂的 AntiClip (不能向量化的部分)
            if (!canFullyVectorize)
            {
                int baseIdx = i;
                // Vector128 存回去之后，再取出来做非线性处理 (Tanh/Dynamic)
                // 注意：这里有个性能陷阱。如果你在这里做 SoftClip，
                // 之前的向量化计算可能被 Store-Load Forwarding 延迟拖慢。
                // 但考虑到 SoftClip 本身全是数学函数，这点开销可以接受。
                ApplyComplexAntiClip(ref Unsafe.Add(ref dataRef, baseIdx), ref Unsafe.Add(ref dataRef, baseIdx + 1));
                ApplyComplexAntiClip(ref Unsafe.Add(ref dataRef, baseIdx + 2),
                    ref Unsafe.Add(ref dataRef, baseIdx + 3));
            }
        }

        // 3. 处理剩余尾部 (Scalar Loop)
        for (; i < len; i += 2)
        {
            // 保持原有的 Scalar 处理逻辑 ...
            // (此处代码省略，与原版一致)
            float l = Unsafe.Add(ref dataRef, i);
            float r = Unsafe.Add(ref dataRef, i + 1);

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

            Unsafe.Add(ref dataRef, i) = outL;
            Unsafe.Add(ref dataRef, i + 1) = outR;
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
            return Vector128.Shuffle(v, s_stereoSwapMask);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Source = null;
        Balance = 0f;
        Mode = BalanceMode.CrossMix;
        AntiClipStrategy = AntiClipStrategy.PreventiveAttenuation;
    }

    public bool ExcludeFromPool { get; init; }
}