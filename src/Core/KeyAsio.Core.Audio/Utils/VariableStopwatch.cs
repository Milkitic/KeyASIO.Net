using System.Diagnostics;

namespace KeyAsio.Core.Audio.Utils;

public class VariableStopwatch
{
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public TimeSpan ManualOffset { get; set; }
    public TimeSpan VariableOffset { get; set; }
    public TimeSpan CalibrationOffset { get; set; }

    public float Rate
    {
        get => _rate;
        set
        {
            SkipTo(Elapsed);
            _rate = value;
        }
    }

    private TimeSpan _skipOffset;
    private float _rate = 1;

    public bool IsRunning => _stopwatch.IsRunning;
    public void Start() => _stopwatch.Start();
    public void Stop() => _stopwatch.Stop();

    public void Restart()
    {
        _skipOffset = TimeSpan.Zero;
        _stopwatch.Restart();
    }

    public void Reset()
    {
        _skipOffset = TimeSpan.Zero;
        _stopwatch.Reset();
    }

    public void SkipTo(TimeSpan startOffset)
    {
        _skipOffset = startOffset;
        if (IsRunning)
        {
            _stopwatch.Restart();
        }
        else
        {
            _stopwatch.Reset();
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            var baseTicks = _stopwatch.ElapsedTicks;
            var scaledTicks = baseTicks * (double)Rate;
            var scaledBaseTime = TimeSpan.FromTicks((long)scaledTicks);

            return scaledBaseTime
                .Add(_skipOffset)
                .Add(ManualOffset)
                .Add(VariableOffset)
                .Add(CalibrationOffset);
        }
    }

    public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;

    public long ElapsedTicks => Elapsed.Ticks;
}