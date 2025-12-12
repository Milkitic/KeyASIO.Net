using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.OsuMemoryModels;
using KeyAsio.Shared.Realtime;

namespace KeyAsio.ViewModels;

public partial class RealtimeDisplayViewModel : ObservableObject, IDisposable
{
    private readonly RealtimeSessionContext _session;
    private readonly DispatcherTimer _timer;

    public RealtimeDisplayViewModel(RealtimeSessionContext session)
    {
        _session = session;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, Tick);
        _timer.Start();
    }

    [ObservableProperty]
    public partial int PlayTime { get; set; }

    [ObservableProperty]
    public partial string SyncedStatusText { get; set; } = "OFFLINE";

    [ObservableProperty]
    public partial int ProcessId { get; set; }

    [ObservableProperty]
    public partial OsuMemoryStatus OsuStatus { get; set; }

    [ObservableProperty]
    public partial BeatmapIdentifier Beatmap { get; set; }

    private void Tick(object? sender, EventArgs e)
    {
        // Poll values from the high-frequency session context
        if (PlayTime != _session.PlayTime)
            PlayTime = _session.PlayTime;

        if (SyncedStatusText != _session.SyncedStatusText)
            SyncedStatusText = _session.SyncedStatusText;

        if (ProcessId != _session.ProcessId)
            ProcessId = _session.ProcessId;

        if (OsuStatus != _session.OsuStatus)
            OsuStatus = _session.OsuStatus;

        if (Beatmap != _session.Beatmap)
            Beatmap = _session.Beatmap;
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}