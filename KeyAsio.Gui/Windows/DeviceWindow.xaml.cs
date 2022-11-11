using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.UserControls;
using Milki.Extensions.MixPlayer.Devices;

namespace KeyAsio.Gui.Windows;

public class DeviceWindowViewModel : ViewModelBase
{
    private List<DeviceDescription>? _devices;
    private DeviceDescription? _selectedDevice;
    private int _latency = 3;
    private int _sampleRate = 44100;
    private bool _isExclusive;
    private ushort _forceAsioBufferSize;

    public List<DeviceDescription>? Devices
    {
        get => _devices;
        set => SetField(ref _devices, value);
    }

    public DeviceDescription? SelectedDevice
    {
        get => _selectedDevice;
        set => SetField(ref _selectedDevice, value);
    }

    public int Latency
    {
        get => _latency;
        set => SetField(ref _latency, value);
    }

    public int SampleRate
    {
        get => _sampleRate;
        set => SetField(ref _sampleRate, value);
    }

    public bool IsExclusive
    {
        get => _isExclusive;
        set => SetField(ref _isExclusive, value);
    }

    public ushort ForceAsioBufferSize
    {
        get => _forceAsioBufferSize;
        set => SetField(ref _forceAsioBufferSize, value);
    }
}

/// <summary>
/// DeviceWindow.xaml 的交互逻辑
/// </summary>
public partial class DeviceWindow : DialogWindow
{
    public DeviceWindowViewModel ViewModel { get; }

    public DeviceWindow()
    {
        InitializeComponent();
        DataContext = ViewModel = new DeviceWindowViewModel();
    }

    private async void DeviceWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Devices = await Task.Run(() => DeviceCreationHelper.GetCachedAvailableDevices()
            .OrderBy(k => k, new DeviceDescriptionComparer())
            .ToList());

        ViewModel.SelectedDevice = ViewModel.Devices.FirstOrDefault(k => k.WavePlayerType == WavePlayerType.ASIO) ??
                                   ViewModel.Devices.FirstOrDefault(k => k.WavePlayerType == WavePlayerType.WASAPI) ??
                                   ViewModel.Devices.FirstOrDefault();
    }

    private void btnConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}

internal class DeviceDescriptionComparer : IComparer<DeviceDescription>
{
    public int Compare(DeviceDescription? x, DeviceDescription? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (ReferenceEquals(null, y)) return 1;
        if (ReferenceEquals(null, x)) return -1;
        var wavePlayerTypeComparison = x.WavePlayerType.CompareTo(y.WavePlayerType);
        if (wavePlayerTypeComparison != 0) return wavePlayerTypeComparison;
        var deviceIdComparison = string.Compare(x.DeviceId, y.DeviceId, StringComparison.Ordinal);
        if (deviceIdComparison != 0) return deviceIdComparison;
        return string.Compare(x.FriendlyName, y.FriendlyName, StringComparison.Ordinal);
    }
}