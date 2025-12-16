using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KeyAsio.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

namespace MemoryReadingTest.Charts;

public class PlayTimeChartVm : ViewModelBase
{

    public PlayTimeChartVm()
    {
        Series = [new StepLineSeries<TimeSpanPoint> { Values = Collection }];
    }

    public ISeries[] Series { get; set; }

    public ObservableCollection<TimeSpanPoint> Collection { get; } = [];

    public Axis[] XAxes { get; set; } =
    [
        new TimeSpanAxis(TimeSpan.FromMilliseconds(1), timeSpan => $"{timeSpan.TotalMilliseconds}ms")
    ];
}

/// <summary>
/// PlayTimeChart.xaml 的交互逻辑
/// </summary>
public partial class PlayTimeChart : UserControl
{
    private readonly PlayTimeChartVm _viewModel;

    public PlayTimeChart()
    {
        InitializeComponent();
        DataContext = _viewModel = new PlayTimeChartVm();

        if (Application.Current is App app)
        {
            app.MemoryScan.MemoryReadObject.PlayingTimeChanged += MemoryReadObject_PlayingTimeChanged;
        }
    }

    private void MemoryReadObject_PlayingTimeChanged(int oldValue, int newValue)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
            _viewModel.Collection.Add(new TimeSpanPoint(TimeSpan.FromMicroseconds(newValue), newValue)));
    }
}
