using KeyAsio.Core.OsuAudio.Hitsounds.Playback;

namespace KeyAsio.Core.OsuAudio.Timeline;

public sealed class PlaybackEventTimelineScheduler
{
    private IReadOnlyList<PlaybackEvent> _events = Array.Empty<PlaybackEvent>();
    private int _nextIndex;
    private TimeSpan _lastPosition;

    public TimeSpan BackwardSeekTolerance { get; set; } = TimeSpan.FromMilliseconds(2);
    public bool AutoSeekOnBackwardJump { get; set; } = true;

    public IReadOnlyList<PlaybackEvent> Events => _events;
    public int NextIndex => _nextIndex;
    public bool IsCompleted => _nextIndex >= _events.Count;

    public void Load(IEnumerable<PlaybackEvent> playbackEvents)
    {
        ArgumentNullException.ThrowIfNull(playbackEvents);

        _events = playbackEvents
            .OrderBy(static k => k.Offset)
            .ToArray();
        _nextIndex = 0;
        _lastPosition = TimeSpan.Zero;
    }

    public void Reset()
    {
        _nextIndex = 0;
        _lastPosition = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        _nextIndex = FindFirstEventAtOrAfter(position.TotalMilliseconds);
        _lastPosition = position;
    }

    public int CollectDueEvents(TimeSpan position, ICollection<PlaybackEvent> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (AutoSeekOnBackwardJump && position + BackwardSeekTolerance < _lastPosition)
        {
            Seek(position);
        }

        _lastPosition = position;

        var dueTime = position.TotalMilliseconds;
        var startCount = destination.Count;

        while (_nextIndex < _events.Count && _events[_nextIndex].Offset <= dueTime)
        {
            destination.Add(_events[_nextIndex]);
            _nextIndex++;
        }

        return destination.Count - startCount;
    }

    private int FindFirstEventAtOrAfter(double offset)
    {
        var left = 0;
        var right = _events.Count;

        while (left < right)
        {
            var middle = left + ((right - left) >> 1);
            if (_events[middle].Offset < offset)
            {
                left = middle + 1;
            }
            else
            {
                right = middle;
            }
        }

        return left;
    }
}
