using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using KeyAsio.MemoryReading;
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

        MemoryScan.MemoryReadObject.PlayingTimeChanged += MemoryReadObject_PlayingTimeChanged;
    }

    private void MemoryReadObject_PlayingTimeChanged(int oldValue, int newValue)
    {
        _viewModel.Collection.Add(new TimeSpanPoint(TimeSpan.FromMicroseconds(newValue), newValue));
    }
}