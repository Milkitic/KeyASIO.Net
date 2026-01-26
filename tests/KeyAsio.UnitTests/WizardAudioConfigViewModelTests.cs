using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KeyAsio.Core.Audio;
using KeyAsio.ViewModels;
using Moq;
using NAudio.Wave;
using SukiUI.Toasts;

namespace KeyAsio.UnitTests;

public class WizardAudioConfigViewModelTests
{
    private readonly Mock<IAudioDeviceManager> _mockDeviceManager;
    private readonly Mock<IPlaybackEngine> _mockPlaybackEngine;
    private readonly Mock<ISukiToastManager> _mockToastManager;

    public WizardAudioConfigViewModelTests()
    {
        _mockDeviceManager = new Mock<IAudioDeviceManager>();
        _mockPlaybackEngine = new Mock<IPlaybackEngine>();
        _mockToastManager = new Mock<ISukiToastManager>();

        // Default setup for device manager
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription>());
    }

    private WizardAudioConfigViewModel CreateViewModel()
    {
        return new WizardAudioConfigViewModel(
            _mockDeviceManager.Object,
            _mockPlaybackEngine.Object,
            _mockToastManager.Object);
    }

    [AvaloniaFact]
    public void Constructor_InitializesDefaults()
    {
        var vm = CreateViewModel();
        Assert.Equal(WizardMode.NotSelected, vm.SelectedMode);
        Assert.Equal(AudioSubStep.Selection, vm.CurrentAudioSubStep);
        Assert.Equal(WavePlayerType.ASIO, vm.SelectedDriverType);
        Assert.Empty(vm.AvailableAudioDevices);
    }

    [AvaloniaFact]
    public void SelectMode_Hardware_ShowsWarningIfNoAsio()
    {
        // Arrange
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription>
            {
                new DeviceDescription { WavePlayerType = WavePlayerType.WASAPI, FriendlyName = "Wasapi Device" }
            });

        var vm = CreateViewModel();

        // Act
        vm.SelectModeCommand.Execute(WizardMode.Hardware);

        // Wait for async dispatcher
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.Equal(WizardMode.Hardware, vm.SelectedMode);
        Assert.True(vm.ShowHardwareDriverWarning);
        Assert.Equal(WavePlayerType.ASIO, vm.SelectedDriverType);
    }

    [AvaloniaFact]
    public void SelectMode_Hardware_NoWarningIfAsioExists()
    {
        // Arrange
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription>
            {
                new DeviceDescription { WavePlayerType = WavePlayerType.ASIO, FriendlyName = "Asio Device" }
            });

        var vm = CreateViewModel();

        // Act
        vm.SelectModeCommand.Execute(WizardMode.Hardware);

        // Wait for async dispatcher
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.False(vm.ShowHardwareDriverWarning);
    }

    [AvaloniaFact]
    public void SelectMode_Software_DefaultsToWasapi()
    {
        var vm = CreateViewModel();
        vm.SelectModeCommand.Execute(WizardMode.Software);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WizardMode.Software, vm.SelectedMode);
        Assert.Equal(WavePlayerType.WASAPI, vm.SelectedDriverType);
    }

    [AvaloniaFact]
    public void LoadDevices_FiltersByDriverType()
    {
        // Arrange
        var asioDevice = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO, FriendlyName = "ASIO" };
        var wasapiDevice = new DeviceDescription { WavePlayerType = WavePlayerType.WASAPI, FriendlyName = "WASAPI" };

        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription> { asioDevice, wasapiDevice });

        var vm = CreateViewModel();

        // Act
        vm.SelectedDriverType = WavePlayerType.ASIO;
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.Single(vm.AvailableAudioDevices);
        Assert.Equal(asioDevice, vm.AvailableAudioDevices.First());
        Assert.Equal(asioDevice, vm.SelectedAudioDevice);

        // Switch to WASAPI
        vm.SelectedDriverType = WavePlayerType.WASAPI;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.AvailableAudioDevices);
        Assert.Equal(wasapiDevice, vm.AvailableAudioDevices.First());
    }

    [AvaloniaFact]
    public void TryGoForward_Configuration_Success()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentAudioSubStep = AudioSubStep.Configuration;
        var device = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO };
        vm.AvailableAudioDevices.Add(device);
        vm.SelectedAudioDevice = device;

        // Act
        bool result = vm.TryGoForward();

        // Assert
        Assert.True(result);
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);
        _mockPlaybackEngine.Verify(e => e.StartDevice(device, null), Times.Once);
    }

    [AvaloniaFact]
    public void TryGoForward_Configuration_Failure()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentAudioSubStep = AudioSubStep.Configuration;
        var device = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO };
        vm.SelectedAudioDevice = device;

        _mockPlaybackEngine.Setup(e => e.StartDevice(It.IsAny<DeviceDescription>(), It.IsAny<WaveFormat>()))
            .Throws(new Exception("Fail"));

        // Act
        bool result = vm.TryGoForward();

        // Assert
        Assert.True(result);
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.False(vm.ValidationSuccess);
        Assert.Contains("Fail", vm.ValidationMessage);
    }

    [AvaloniaFact]
    public void VirtualDriver_Detection_Success()
    {
        // Arrange
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription>
            {
                new DeviceDescription
                    { WavePlayerType = WavePlayerType.WASAPI, FriendlyName = "CABLE Input (VB-Audio Virtual Cable)" }
            });

        var vm = CreateViewModel();

        // Act
        vm.RetryVirtualDriverCheckCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.True(vm.IsVirtualDriverDetected);
    }

    [AvaloniaFact]
    public void Integration_HardwareFlow_Full()
    {
        // Arrange
        var asioDevice = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO, FriendlyName = "Real ASIO" };
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription> { asioDevice });

        var vm = CreateViewModel();

        // 1. Select Mode
        vm.SelectModeCommand.Execute(WizardMode.Hardware);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(AudioSubStep.Configuration, vm.CurrentAudioSubStep);
        Assert.Equal(WavePlayerType.ASIO, vm.SelectedDriverType);

        // 2. Select Device (Auto selected by LoadDevices logic)
        Assert.Equal(asioDevice, vm.SelectedAudioDevice);
        Assert.True(vm.CanGoForward);

        // 3. Go Forward (Validation)
        vm.TryGoForward();
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);

        // 4. Go Forward (Finish)
        bool result = vm.TryGoForward();
        Assert.False(result); // Should return false to indicate proceeding to next main wizard step
    }

    [AvaloniaFact]
    public void Integration_ProMixSoftwareFlow_Full()
    {
        // Arrange
        var cableDevice = new DeviceDescription
            { WavePlayerType = WavePlayerType.WASAPI, FriendlyName = "CABLE Input (VB-Audio Virtual Cable)" };
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription> { cableDevice });

        var vm = CreateViewModel();

        // 1. Select Mode -> Software
        vm.SelectModeCommand.Execute(WizardMode.Software);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WizardMode.Software, vm.SelectedMode);
        Assert.Equal(AudioSubStep.Configuration, vm.CurrentAudioSubStep);

        // 2. Virtual Driver Check (Happens automatically in SelectMode(Software))
        Assert.True(vm.IsVirtualDriverDetected);

        // 3. Driver defaults to WASAPI
        Assert.Equal(WavePlayerType.WASAPI, vm.SelectedDriverType);

        // 4. Device Selection (Auto)
        Assert.Equal(cableDevice, vm.SelectedAudioDevice);
        Assert.True(vm.CanGoForward);

        // 5. Go Forward (Validation)
        vm.TryGoForward();
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);

        // 6. Go Forward (Finish)
        bool result = vm.TryGoForward();
        Assert.False(result);
    }
}