using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace KeyAsio.Audio.Utils;

public static class SimdAudioConverter
{
    private const int Avx512VectorSize = 32;
    private const int Avx2VectorSize = 16;
    private const float ScalingFactor = 1.0f / 32768f; // 预计算乘法因子，乘法比除法快

    public static bool EnableAvx512 { get; set; } = true;

    public static unsafe void Convert16BitToFloatUnsafe(short* pSource, float* pDest, int sampleCount)
    {
        var i = 0;

        if (Vector512.IsHardwareAccelerated && EnableAvx512)
        {
            var vScale = Vector512.Create(ScalingFactor);

            while (i <= sampleCount - Avx512VectorSize)
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

                i += Avx512VectorSize;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            var vScale = Vector256.Create(ScalingFactor);

            // 2x 循环展开：每次处理 32 个采样
            while (i <= sampleCount - Avx2VectorSize * 2)
            {
                var v1 = Vector256.Load(pSource + i);
                var v2 = Vector256.Load(pSource + i + Avx2VectorSize);

                // 第一组
                var (lowInts1, highInts1) = Vector256.Widen(v1);
                var lowFloats1 = Vector256.ConvertToSingle(lowInts1) * vScale;
                var highFloats1 = Vector256.ConvertToSingle(highInts1) * vScale;

                lowFloats1.Store(pDest + i);
                highFloats1.Store(pDest + i + 8);

                // 第二组
                var (lowInts2, highInts2) = Vector256.Widen(v2);
                var lowFloats2 = Vector256.ConvertToSingle(lowInts2) * vScale;
                var highFloats2 = Vector256.ConvertToSingle(highInts2) * vScale;

                lowFloats2.Store(pDest + i + Avx2VectorSize);
                highFloats2.Store(pDest + i + Avx2VectorSize + 8);

                i += Avx2VectorSize * 2;
            }

            while (i <= sampleCount - Avx2VectorSize)
            {
                var v = Vector256.Load(pSource + i);
                var (lowInt, highInt) = Vector256.Widen(v);
                (Vector256.ConvertToSingle(lowInt) * vScale).Store(pDest + i);
                (Vector256.ConvertToSingle(highInt) * vScale).Store(pDest + i + 8);
                i += Avx2VectorSize;
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