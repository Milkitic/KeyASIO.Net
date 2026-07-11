using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KeyAsio.Core.Audio;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.ViewModels;
using Moq;
using SukiUI.Toasts;

namespace KeyAsio.UnitTests;

public class WizardAudioConfigViewModelTests
{
    private readonly Mock<IAudioDeviceManager> _mockDeviceManager;
    private readonly Mock<IAudioDeviceOperationCoordinator> _mockDeviceOperations;
    private readonly Mock<ISukiToastManager> _mockToastManager;
    private readonly AppSettings _appSettings;

    public WizardAudioConfigViewModelTests()
    {
        _mockDeviceManager = new Mock<IAudioDeviceManager>();
        _mockDeviceOperations = new Mock<IAudioDeviceOperationCoordinator>();
        _mockToastManager = new Mock<ISukiToastManager>();
        _appSettings = new AppSettings();

        // Default setup for device manager
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription>());
        _mockDeviceOperations
            .Setup(x => x.ApplyAsync(
                It.IsAny<DeviceDescription?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((DeviceDescription? device, int sampleRate, CancellationToken cancellationToken) =>
                Task.FromResult(new AudioDeviceOperationResult(true, device)));
    }

    private WizardAudioConfigViewModel CreateViewModel()
    {
        return new WizardAudioConfigViewModel(
            _mockDeviceManager.Object,
            _mockDeviceOperations.Object,
            _mockToastManager.Object,
            _appSettings);
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
                new() { WavePlayerType = WavePlayerType.ASIO, FriendlyName = "Asio Device" }
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
    public void SelectMode_UpdatesIsHardwareMode()
    {
        var vm = CreateViewModel();

        // Initial
        Assert.False(vm.IsHardwareMode);

        // Select Hardware
        vm.SelectModeCommand.Execute(WizardMode.Hardware);
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsHardwareMode);

        // Select Software
        vm.SelectModeCommand.Execute(WizardMode.Software);
        Dispatcher.UIThread.RunJobs();
        Assert.False(vm.IsHardwareMode);
    }

    [AvaloniaFact]
    public void SelectMode_UpdatesEnableMixSync()
    {
        var vm = CreateViewModel();

        // Default should be whatever AppSettings default is (likely false or true depending on initialization, but we can set it explicitly to test toggling)
        _appSettings.Sync.EnableMixSync = true;

        // Select Hardware (Manual) -> should become false
        vm.SelectModeCommand.Execute(WizardMode.Hardware);
        Dispatcher.UIThread.RunJobs();
        Assert.False(_appSettings.Sync.EnableMixSync);

        // Select Software (ProMix) -> should become true
        vm.SelectModeCommand.Execute(WizardMode.Software);
        Dispatcher.UIThread.RunJobs();
        Assert.True(_appSettings.Sync.EnableMixSync);
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
    public async Task TryGoForward_Configuration_Success()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentAudioSubStep = AudioSubStep.Configuration;
        var device = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO };
        vm.AvailableAudioDevices.Add(device);
        vm.SelectedAudioDevice = device;

        // Act
        bool result = await vm.TryGoForwardAsync();

        // Assert
        Assert.True(result);
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);
        _mockDeviceOperations.Verify(x => x.ApplyAsync(
            device,
            _appSettings.Audio.SampleRate,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [AvaloniaFact]
    public async Task TryGoForward_Configuration_Failure()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentAudioSubStep = AudioSubStep.Configuration;
        var device = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO };
        vm.SelectedAudioDevice = device;

        _mockDeviceOperations
            .Setup(x => x.ApplyAsync(
                It.IsAny<DeviceDescription?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioDeviceOperationResult(false, null, new Exception("Fail")));

        // Act
        bool result = await vm.TryGoForwardAsync();

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
    public async Task Integration_HardwareFlow_Full()
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
        await vm.TryGoForwardAsync();
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);

        // 4. Go Forward (Finish)
        bool result = await vm.TryGoForwardAsync();
        Assert.False(result); // Should return false to indicate proceeding to next main wizard step
    }

    [AvaloniaFact]
    public async Task Integration_ProMixSoftwareFlow_Full()
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
        await vm.TryGoForwardAsync();
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);

        // 6. Go Forward (Finish)
        bool result = await vm.TryGoForwardAsync();
        Assert.False(result);
    }
}
