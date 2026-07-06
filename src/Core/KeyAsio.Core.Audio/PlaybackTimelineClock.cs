using System.Diagnostics;

namespace KeyAsio.Core.Audio;

internal sealed class PlaybackTimelineClock
{
    private readonly Lock _gate = new();
    private readonly Func<long> _getTimestamp;
    private readonly double _ticksPerTimestamp;
    private readonly TimeSpan _duration;
    private TimeSpan _basePosition;
    private long _startTimestamp;
    private float _rate = 1;
    private bool _isLooping;
    private bool _isRunning;

    public PlaybackTimelineClock(TimeSpan duration)
        : this(duration, Stopwatch.GetTimestamp, Stopwatch.Frequency)
    {
    }

    internal PlaybackTimelineClock(TimeSpan duration, Func<long> getTimestamp, long timestampFrequency)
    {
        ArgumentNullException.ThrowIfNull(getTimestamp);
        if (timestampFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        }

        _duration = duration;
        _getTimestamp = getTimestamp;
        _ticksPerTimestamp = (double)TimeSpan.TicksPerSecond / timestampFrequency;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _isRunning;
            }
        }
    }

    public bool IsLooping
    {
        get
        {
            lock (_gate)
            {
                return _isLooping;
            }
        }
        set
        {
            lock (_gate)
            {
                _basePosition = GetPositionNoLock();
                _isLooping = value;
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_gate)
            {
                return GetPositionNoLock();
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_isRunning) return;
            _basePosition = Normalize(_basePosition);
            _startTimestamp = _getTimestamp();
            _isRunning = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_isRunning) return;
            _basePosition = GetPositionNoLock();
            _isRunning = false;
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            _basePosition = Normalize(position);
            if (_isRunning)
            {
                _startTimestamp = _getTimestamp();
            }
        }
    }

    public void SetRate(float rate)
    {
        lock (_gate)
        {
            _basePosition = GetPositionNoLock();
            _rate = rate;
            if (_isRunning)
            {
                _startTimestamp = _getTimestamp();
            }
        }
    }

    private TimeSpan GetPositionNoLock()
    {
        if (!_isRunning)
        {
            return Normalize(_basePosition);
        }

        var elapsedTicks = (_getTimestamp() - _startTimestamp) * _ticksPerTimestamp;
        var scaledTicks = elapsedTicks * _rate;
        return Normalize(_basePosition + TimeSpan.FromTicks((long)scaledTicks));
    }

    private TimeSpan Normalize(TimeSpan position)
    {
        if (_duration <= TimeSpan.Zero)
        {
            return position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }

        if (_isLooping)
        {
            var ticks = position.Ticks % _duration.Ticks;
            if (ticks < 0)
            {
                ticks += _duration.Ticks;
            }

            return TimeSpan.FromTicks(ticks);
        }

        if (position <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return position >= _duration ? _duration : position;
    }
}
