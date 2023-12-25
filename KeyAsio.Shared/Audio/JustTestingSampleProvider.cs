#if DEBUG
using NAudio.Wave;

namespace KeyAsio.Shared.Audio;

internal class JustTestingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;

    private float[] _buffer = new float[40960];
    private float[] _bufferCopy = new float[40960];

    private int _currentIndex;
    private int _currentCount;

    public JustTestingSampleProvider(ISampleProvider sourceProvider)
    {
        _sourceProvider = sourceProvider;
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;
        if (_currentIndex >= _currentCount)
        {
            var samplesRead = _sourceProvider.Read(_bufferCopy, offset, count);
            for (var i = 0; i < count * 2; i += 2)
            {
                _buffer[i] = _bufferCopy[i / 2];
                _buffer[i + 1] = _bufferCopy[i / 2];
            }

            _currentIndex = 0;
            _currentCount = samplesRead * 2;
        }

        if (_currentCount == 0) return 0;

        var c = _currentCount / 2;
        var len = Math.Min(c, _currentCount - _currentIndex);
        _buffer.AsSpan().Slice(_currentIndex, len).CopyTo(buffer);
        _currentIndex += len;
        return len;
    }
}
#endif