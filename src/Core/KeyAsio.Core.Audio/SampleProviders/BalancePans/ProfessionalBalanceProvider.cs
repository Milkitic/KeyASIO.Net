using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using KeyAsio.Core.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.BalancePans;

/// <summary>
/// Applies stereo panning and sound-field matrices. Clipping protection belongs to the final mixer limiter.
/// </summary>
public sealed class ProfessionalBalanceProvider : IRecyclableProvider, IPoolable
{
    public static bool EnableAvx512 { get; set; } = true;

    private static readonly bool s_canUseVectorization =
        Vector128.IsHardwareAccelerated &&
        (Sse.IsSupported || AdvSimd.Arm64.IsSupported);

    private static readonly Vector128<int> s_stereoSwapMask;
    private static readonly Vector256<int> s_swapMask256;
    private static readonly Vector512<int> s_swapMask512;
    private static readonly Vector128<float> s_vHalf;

    static ProfessionalBalanceProvider()
    {
        if (s_canUseVectorization)
        {
            s_stereoSwapMask = Vector128.Create(1, 0, 3, 2);
            s_vHalf = Vector128.Create(0.5f);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            s_swapMask256 = Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6);
        }

        if (Vector512.IsHardwareAccelerated && EnableAvx512)
        {
            s_swapMask512 = Vector512.Create(1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14);
        }
    }

    private float _balanceValue;
    private BalanceMode _mode = BalanceMode.ProMixFocus;

    private float _leftDirectGain;
    private float _rightDirectGain;
    private float _leftCrossGain;
    private float _rightCrossGain;

    private Vector128<float> _vDirectGain;
    private Vector128<float> _vCrossGain;

    public ProfessionalBalanceProvider()
    {
        UpdateGains();
    }

    public ProfessionalBalanceProvider(ISampleProvider? sourceProvider, BalanceMode mode = BalanceMode.ProMixFocus)
    {
        _mode = mode;
        Source = sourceProvider;
        UpdateGains();
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
                UpdateEqualPowerPanGains();
                break;
            case BalanceMode.ProMixFocus:
                UpdateProMixFocusGains();
                break;
            case BalanceMode.LinearStereoPan:
                UpdateLinearStereoPanGains();
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
    private void UpdateEqualPowerPanGains()
    {
        if (_balanceValue < 0)
        {
            float angle = -_balanceValue * MathF.PI * 0.5f;
            _leftDirectGain = 1f;
            _leftCrossGain = MathF.Sin(angle);
            _rightDirectGain = MathF.Cos(angle);
            _rightCrossGain = 0f;
            return;
        }

        if (_balanceValue > 0)
        {
            float angle = _balanceValue * MathF.PI * 0.5f;
            _leftDirectGain = MathF.Cos(angle);
            _leftCrossGain = 0f;
            _rightDirectGain = 1f;
            _rightCrossGain = MathF.Sin(angle);
            return;
        }

        UpdateOffGains();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateProMixFocusGains() => UpdateOffGains();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLinearStereoPanGains()
    {
        if (_balanceValue < 0)
        {
            float amount = -_balanceValue;
            _leftDirectGain = 1f;
            _leftCrossGain = amount;
            _rightDirectGain = 1f - amount;
            _rightCrossGain = 0f;
            return;
        }

        if (_balanceValue > 0)
        {
            float amount = _balanceValue;
            _leftDirectGain = 1f - amount;
            _leftCrossGain = 0f;
            _rightDirectGain = 1f;
            _rightCrossGain = amount;
            return;
        }

        UpdateOffGains();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateOffGains()
    {
        _leftDirectGain = 1f;
        _rightDirectGain = 1f;
        _leftCrossGain = 0f;
        _rightCrossGain = 0f;
    }

    public int Read(float[] buffer, int offset, int sampleCount)
    {
        if (Source == null)
        {
            Array.Clear(buffer, offset, sampleCount);
            return sampleCount;
        }

        if (sampleCount == 0) return 0;
        int samplesRead = Source.Read(buffer, offset, sampleCount);

        if (_balanceValue == 0 || _mode == BalanceMode.Off)
        {
            return samplesRead;
        }

        if (_mode == BalanceMode.ProMixFocus)
        {
            if (s_canUseVectorization)
            {
                ProcessProMixFocusVectorized(buffer.AsSpan(offset, samplesRead));
            }
            else
            {
                ProcessProMixFocusSafe(buffer, offset, samplesRead);
            }
        }
        else if (s_canUseVectorization)
        {
            ProcessStandardVectorized(buffer.AsSpan(offset, samplesRead));
        }
        else
        {
            ProcessStandardSafe(buffer, offset, samplesRead);
        }

        return samplesRead;
    }

    private void ProcessStandardVectorized(Span<float> data)
    {
        var vecSpan = MemoryMarshal.Cast<float, Vector128<float>>(data);
        int i = 0;
        for (; i < vecSpan.Length; i++)
        {
            Vector128<float> input = vecSpan[i];
            Vector128<float> swapped = SwapStereoChannels(input);
            vecSpan[i] = (input * _vDirectGain) + (swapped * _vCrossGain);
        }

        for (int j = i * Vector128<float>.Count; j < data.Length; j += 2)
        {
            float left = data[j];
            float right = data[j + 1];
            data[j] = left * _leftDirectGain + right * _leftCrossGain;
            data[j + 1] = right * _rightDirectGain + left * _rightCrossGain;
        }
    }

    private void ProcessProMixFocusVectorized(Span<float> data)
    {
        ref float dataRef = ref MemoryMarshal.GetReference(data);
        int length = data.Length;
        float sideGain = 1f - Math.Abs(_balanceValue);
        float midGainL = 1f - _balanceValue * 0.5f;
        float midGainR = 1f + _balanceValue * 0.5f;
        int i = 0;

        if (Vector512.IsHardwareAccelerated && EnableAvx512)
        {
            var midGain = Vector512.Create(midGainL, midGainR, midGainL, midGainR, midGainL, midGainR, midGainL,
                midGainR, midGainL, midGainR, midGainL, midGainR, midGainL, midGainR, midGainL, midGainR);
            var sideGainVector = Vector512.Create(sideGain);
            var half = Vector512.Create(0.5f);

            for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                Vector512<float> input = Vector512.LoadUnsafe(ref dataRef, (nuint)i);
                Vector512<float> swapped = Vector512.Shuffle(input, s_swapMask512);
                Vector512<float> mid = (input + swapped) * half;
                Vector512<float> side = (input - swapped) * half;
                ((mid * midGain) + (side * sideGainVector)).StoreUnsafe(ref dataRef, (nuint)i);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            var midGain = Vector256.Create(midGainL, midGainR, midGainL, midGainR, midGainL, midGainR, midGainL,
                midGainR);
            var sideGainVector = Vector256.Create(sideGain);
            var half = Vector256.Create(0.5f);

            for (; i <= length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                Vector256<float> input = Vector256.LoadUnsafe(ref dataRef, (nuint)i);
                Vector256<float> swapped = Vector256.Shuffle(input, s_swapMask256);
                Vector256<float> mid = (input + swapped) * half;
                Vector256<float> side = (input - swapped) * half;
                ((mid * midGain) + (side * sideGainVector)).StoreUnsafe(ref dataRef, (nuint)i);
            }
        }

        var midGain128 = Vector128.Create(midGainL, midGainR, midGainL, midGainR);
        var sideGain128 = Vector128.Create(sideGain);
        for (; i <= length - Vector128<float>.Count; i += Vector128<float>.Count)
        {
            Vector128<float> input = Vector128.LoadUnsafe(ref dataRef, (nuint)i);
            Vector128<float> swapped = SwapStereoChannels(input);
            Vector128<float> mid = (input + swapped) * s_vHalf;
            Vector128<float> side = (input - swapped) * s_vHalf;
            ((mid * midGain128) + (side * sideGain128)).StoreUnsafe(ref dataRef, (nuint)i);
        }

        for (; i < length; i += 2)
        {
            float left = Unsafe.Add(ref dataRef, i);
            float right = Unsafe.Add(ref dataRef, i + 1);
            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f * sideGain;

            Unsafe.Add(ref dataRef, i) = mid * midGainL + side;
            Unsafe.Add(ref dataRef, i + 1) = mid * midGainR - side;
        }
    }

    private void ProcessStandardSafe(float[] buffer, int offset, int count)
    {
        int endIndex = offset + count;
        for (int i = offset; i < endIndex; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];
            buffer[i] = left * _leftDirectGain + right * _leftCrossGain;
            buffer[i + 1] = right * _rightDirectGain + left * _rightCrossGain;
        }
    }

    private void ProcessProMixFocusSafe(float[] buffer, int offset, int count)
    {
        int endIndex = offset + count;
        float sideGain = 1f - Math.Abs(_balanceValue);
        float midGainL = 1f - _balanceValue * 0.5f;
        float midGainR = 1f + _balanceValue * 0.5f;

        for (int i = offset; i < endIndex; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];
            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f * sideGain;
            buffer[i] = mid * midGainL + side;
            buffer[i + 1] = mid * midGainR - side;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> SwapStereoChannels(Vector128<float> value)
    {
        if (Ssse3.IsSupported)
        {
            return Vector128.Shuffle(value, s_stereoSwapMask);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            var odds = AdvSimd.Arm64.UnzipOdd(value, value);
            var evens = AdvSimd.Arm64.UnzipEven(value, value);
            return AdvSimd.Arm64.ZipLow(odds, evens);
        }

        return Vector128.Create(value[1], value[0], value[3], value[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Source = null;
        Balance = 0f;
        Mode = BalanceMode.ProMixFocus;
    }

    public bool ExcludeFromPool { get; init; }
}
