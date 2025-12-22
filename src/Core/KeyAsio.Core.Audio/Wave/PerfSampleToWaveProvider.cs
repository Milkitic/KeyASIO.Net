using NAudio.Wave;

namespace KeyAsio.Audio.Wave;

public class PerfSampleToWaveProvider : IWaveProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveBuffer _reusableWaveBuffer = new([]);

    public PerfSampleToWaveProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Must be already floating point");
        _source = source;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        _reusableWaveBuffer.BindTo(buffer);
        return _source.Read(_reusableWaveBuffer.FloatBuffer, offset >> 2, count >> 2) << 2;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;
}