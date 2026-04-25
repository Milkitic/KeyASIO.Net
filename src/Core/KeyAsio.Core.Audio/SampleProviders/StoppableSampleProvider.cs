using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders;

public sealed class StoppableSampleProvider : IRecyclableProvider
{
    private int _stopped;

    public StoppableSampleProvider(ISampleProvider source)
    {
        Source = source;
    }

    public ISampleProvider? Source { get; private set; }

    public WaveFormat WaveFormat => Source?.WaveFormat ?? throw new InvalidOperationException("Source not ready");

    public void Stop()
    {
        Interlocked.Exchange(ref _stopped, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var source = Source;
        if (source == null || Volatile.Read(ref _stopped) != 0)
        {
            return 0;
        }

        return source.Read(buffer, offset, count);
    }

    public ISampleProvider? ResetAndGetSource()
    {
        var child = Source;
        Source = null;
        _stopped = 0;
        return child;
    }
}
