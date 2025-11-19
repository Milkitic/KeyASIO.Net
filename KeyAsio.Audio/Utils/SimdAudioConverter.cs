using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace KeyAsio.Audio.Utils;

public static class SimdAudioConverter
{
    // 预计算乘法因子，乘法比除法快
    private const float ScalingFactor = 1.0f / 32768f;

    public static bool EnableAvx512 { get; set; } = true;

    public static unsafe void Convert16BitToFloatUnsafe(short* pSource, float* pDest, int sampleCount)
    {
        var i = 0;

        if (Avx512F.IsSupported && EnableAvx512)
        {
            const int vectorSize = 32;
            var vScale = Vector512.Create(ScalingFactor);

            while (i <= sampleCount - vectorSize)
            {
                var sourceVector = Vector512.Load(pSource + i);

                // Widen: 32个short -> 两个包含16个int的Vector512
                // lowInts:  前16个采样 (512 bits)
                // highInts: 后16个采样 (512 bits)
                var (lowInts, highInts) = Vector512.Widen(sourceVector);

                // Int -> Float 并缩放
                var lowFloats = Vector512.ConvertToSingle(lowInts) * vScale;
                var highFloats = Vector512.ConvertToSingle(highInts) * vScale;

                // Store: 写入 16个 float (512 bits)
                lowFloats.Store(pDest + i);
                highFloats.Store(pDest + i + 16);

                i += vectorSize;
            }
        }

        if (Vector.IsHardwareAccelerated)
        {
            // Vector<short>.Count: AVX2=16, SSE2=8
            var vectorSize = Vector<short>.Count;

            while (i <= sampleCount - vectorSize)
            {
                var sourceVector = Unsafe.Read<Vector<short>>(pSource + i);

                // Widen: Short -> Int
                Vector.Widen(sourceVector, out var lowInts, out var highInts);

                // Int -> Float 并缩放
                var lowFloats = Vector.ConvertToSingle(lowInts) * ScalingFactor;
                var highFloats = Vector.ConvertToSingle(highInts) * ScalingFactor;

                // Vector<float>.Count 是 Vector<short>.Count 的一半
                // 比如 Short向量是16个，那么 Float向量是8个，需要写两次
                Unsafe.Write(pDest + i, lowFloats);
                Unsafe.Write(pDest + i + Vector<float>.Count, highFloats);

                i += vectorSize;
            }
        }

        for (; i < sampleCount; i++)
        {
            pDest[i] = pSource[i] * ScalingFactor;
        }
    }
}