using System.Numerics;
using System.Runtime.CompilerServices;

namespace KeyAsio.Audio.Utils;

public static class SimdAudioConverter
{
    // 预计算乘法因子，乘法比除法快
    private const float ScalingFactor = 1.0f / 32768f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Convert16BitToFloatUnsafe(short* pSource, float* pDest, int sampleCount)
    {
        int i = 0;

        if (Vector.IsHardwareAccelerated)
        {
            // Vector<short>.Count: AVX2=16, SSE2=8
            int vectorShortCount = Vector<short>.Count;

            while (i <= sampleCount - vectorShortCount)
            {
                var sourceVector = Unsafe.Read<Vector<short>>(pSource + i);

                // Widen: Short -> Int
                Vector.Widen(sourceVector, out Vector<int> lowInts, out Vector<int> highInts);

                // Int -> Float 并缩放
                var lowFloats = Vector.ConvertToSingle(lowInts) * ScalingFactor;
                var highFloats = Vector.ConvertToSingle(highInts) * ScalingFactor;

                // Vector<float>.Count 是 Vector<short>.Count 的一半
                // 比如 Short向量是16个，那么 Float向量是8个，需要写两次
                Unsafe.Write(pDest + i, lowFloats);
                Unsafe.Write(pDest + i + Vector<float>.Count, highFloats);

                i += vectorShortCount;
            }
        }

        for (; i < sampleCount; i++)
        {
            pDest[i] = pSource[i] * ScalingFactor;
        }
    }
}