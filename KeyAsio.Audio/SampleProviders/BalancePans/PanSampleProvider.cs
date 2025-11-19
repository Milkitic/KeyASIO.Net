using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.BalancePans;

public sealed class PanSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;
    private float _balanceValue = 0f;
    private readonly int _channels;

    public PanSampleProvider(ISampleProvider sourceProvider)
    {
        _sourceProvider = sourceProvider;
        _channels = _sourceProvider.WaveFormat.Channels;
        if (_channels > 2) throw new NotSupportedException("channels: " + _channels);
        Balance = 0f;
    }

    public float Balance
    {
        get => _balanceValue;
        set
        {
            FixBalanceRange(ref value);
            _balanceValue = value;
        }
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;
        int samplesRead = _sourceProvider.Read(buffer, offset, count);
        if (_channels != 2) return samplesRead;
        if (_balanceValue == 0) return samplesRead;
        for (int n = 0; n < count; n += 2)
        {
            var leftIndex = offset + n;
            var rightIndex = leftIndex + 1;
            var left = buffer[leftIndex];
            var right = buffer[rightIndex];

            // 1. 计算单声道混合（永远不会削波）
            var monoSum = (left + right) * 0.5f;
            if (_balanceValue < 0)
            {
                var panAmount = -_balanceValue;

                // 左声道 = 在 原始L 和 monoSum 之间插值
                buffer[leftIndex] = left * (1.0f - panAmount) + monoSum * panAmount;
                // 右声道 = 线性衰减
                buffer[rightIndex] = right * (1.0f - panAmount);
            }
            else // _balanceValue > 0
            {
                var panAmount = _balanceValue;

                // 左声道 = 线性衰减
                buffer[leftIndex] = left * (1.0f - panAmount);
                // 右声道 = 在 原始R 和 monoSum 之间插值
                buffer[rightIndex] = right * (1.0f - panAmount) + monoSum * panAmount;
            }
        }

        return samplesRead;
    }

    private static void FixBalanceRange(ref float value)
    {
        if (value > 1f)
        {
            value = 1f;
        }
        else if (value < -1f)
        {
            value = -1f;
        }
    }
}