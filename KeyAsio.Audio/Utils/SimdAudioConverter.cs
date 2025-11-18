using System.Numerics;

namespace KeyAsio.Audio.Utils;

public static class SimdAudioConverter
{
    // 预计算乘法因子，乘法比除法快
    private const float ScalingFactor = 1.0f / 32768f;

    public static void Convert16BitToFloat(ReadOnlySpan<short> source, Span<float> destination)
    {
        int i = 0;

        // 检查硬件加速是否可用
        if (Vector.IsHardwareAccelerated)
        {
            // Vector<short>.Count 通常是 8 (128-bit) 或 16 (256-bit)
            int vectorSize = Vector<short>.Count;

            // 必须确保源和目标都足够长
            while (i <= source.Length - vectorSize)
            {
                // 1. 加载 Short 向量
                var sourceVector = new Vector<short>(source.Slice(i));

                // 2. 将 Short 扩展为 Int (因为 Short->Float 精度提升，宽度翻倍)
                Vector.Widen(sourceVector, out Vector<int> lowInts, out Vector<int> highInts);

                // 3. 将 Int 转 Float 并乘以缩放因子
                var lowFloats = Vector.ConvertToSingle(lowInts) * ScalingFactor;
                var highFloats = Vector.ConvertToSingle(highInts) * ScalingFactor;

                // 4. 写入目标 (Vector<int> 变成了两个 Vector<float>)
                // Vector<float>.Count 是 Vector<short>.Count 的一半
                lowFloats.CopyTo(destination.Slice(i));
                highFloats.CopyTo(destination.Slice(i + Vector<float>.Count));

                i += vectorSize;
            }
        }

        // 处理剩余的数据 (或者不支持 SIMD 的机器)
        for (; i < source.Length; i++)
        {
            destination[i] = source[i] * ScalingFactor;
        }
    }
}