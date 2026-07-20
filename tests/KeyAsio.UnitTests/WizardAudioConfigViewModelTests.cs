using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KeyAsio.Core.Audio;
using KeyAsio.Configuration;
using KeyAsio.Services;
using KeyAsio.Plugins.Contracts;
using KeyAsio.ViewModels;
using Moq;
using SukiUI.Toasts;

namespace KeyAsio.UnitTests;

public class WizardAudioConfigViewModelTests
{
    private readonly Mock<IAudioDeviceManager> _mockDeviceManager;
    private readonly Mock<IAudioDeviceOperationCoordinator> _mockDeviceOperations;
    private readonly Mock<ISukiToastManager> _mockToastManager;
    private readonly Mock<IPluginManager> _mockPluginManager;
    private readonly Mock<IWizardTestSoundService> _mockTestSoundService;
    private readonly AppSettings _appSettings;

    public WizardAudioConfigViewModelTests()
    {
        _mockDeviceManager = new Mock<IAudioDeviceManager>();
        _mockDeviceOperations = new Mock<IAudioDeviceOperationCoordinator>();
        _mockToastManager = new Mock<ISukiToastManager>();
        _mockPluginManager = new Mock<IPluginManager>();
        _mockTestSoundService = new Mock<IWizardTestSoundService>();
        _appSettings = new AppSettings();

        var proMixPlugin = new Mock<IPlugin>();
        proMixPlugin.SetupGet(plugin => plugin.Id).Returns("KeyAsio.Plugins.ProMix");
        _mockPluginManager.Setup(manager => manager.GetAllPlugins()).Returns([proMixPlugin.Object]);

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
            _appSettings,
            _mockPluginManager.Object,
            _mockTestSoundService.Object);
    }

    [AvaloniaFact]
    public void Constructor_InitializesDefaults()
    {
        var vm = CreateViewModel();
        Assert.Equal(WizardMode.NotSelected, vm.SelectedMode);
        Assert.Equal(AudioSubStep.Selection, vm.CurrentAudioSubStep);
        Assert.Equal(WavePlayerType.ASIO, vm.SelectedDriverType);
        Assert.Empty(vm.AvailableAudioDevices);
        Assert.True(vm.IsProMixAvailable);
    }

    [AvaloniaFact]
    public void ProMixOption_IsDisabled_WhenPluginIsNotLoaded()
    {
        _mockPluginManager.Setup(manager => manager.GetAllPlugins()).Returns([]);

        var vm = CreateViewModel();

        Assert.False(vm.IsProMixAvailable);
        Assert.False(vm.SelectModeCommand.CanExecute(WizardMode.Software));
        Assert.True(vm.SelectModeCommand.CanExecute(WizardMode.Hardware));
    }

    [AvaloniaFact]
    public void SelectMode_Hardware_AllowsWasapiExclusiveWhenNoAsioExists()
    {
        // Arrange
        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription>
            {
                new DeviceDescription
                {
                    WavePlayerType = WavePlayerType.WASAPI,
                    DeviceId = "wasapi-device",
                    FriendlyName = "Wasapi Device"
                }
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

        vm.SelectedDriverType = WavePlayerType.WASAPI;
        Assert.NotNull(vm.SelectedAudioDevice);
        Assert.True(vm.SelectedAudioDevice.IsExclusive);
        Assert.Equal(3, vm.SelectedAudioDevice.Latency);
        Assert.False(vm.ShowHardwareDriverWarning);
    }

    [AvaloniaFact]
    public void SelectMode_Hardware_ShowsWarningWhenSelectedBackendHasNoDevice()
    {
        var vm = CreateViewModel();

        vm.SelectModeCommand.Execute(WizardMode.Hardware);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowHardwareDriverWarning);
        Assert.Null(vm.SelectedAudioDevice);
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
        var wasapiDevice = new DeviceDescription
        {
            WavePlayerType = WavePlayerType.WASAPI,
            DeviceId = "wasapi-device",
            FriendlyName = "WASAPI"
        };

        _mockDeviceManager.Setup(m => m.GetCachedAvailableDevicesAsync())
            .ReturnsAsync(new List<DeviceDescription> { asioDevice, wasapiDevice });

        var vm = CreateViewModel();
        vm.SelectModeCommand.Execute(WizardMode.Hardware);

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
        Assert.Equal(wasapiDevice.FriendlyName, vm.AvailableAudioDevices.First().FriendlyName);
        Assert.True(vm.AvailableAudioDevices.First().IsExclusive);
        Assert.Equal(3, vm.AvailableAudioDevices.First().Latency);
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

        // 3. Create the device and start the once-per-second snare test.
        await vm.TryGoForwardAsync();
        Assert.Equal(AudioSubStep.ConcurrencyCheck, vm.CurrentAudioSubStep);
        Assert.True(vm.IsConcurrencyTestSoundPlaying);
        _mockTestSoundService.Verify(service => service.Start(), Times.Once);

        // 4. Confirm that osu! can still play through the same device.
        vm.ConfirmSameDeviceAudioCommand.Execute(null);
        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);
        Assert.False(vm.IsConcurrencyTestSoundPlaying);
        _mockTestSoundService.Verify(service => service.Stop(), Times.AtLeastOnce);

        // 5. Go Forward (Finish)
        bool result = await vm.TryGoForwardAsync();
        Assert.False(result); // Should return false to indicate proceeding to next main wizard step
    }

    [AvaloniaFact]
    public async Task HardwareFlow_WhenOnlyDeviceCannotRunConcurrently_RequiresProMix()
    {
        var asioDevice = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO, FriendlyName = "ASIO4ALL" };
        var onlyWasapiDevice = new DeviceDescription
        {
            WavePlayerType = WavePlayerType.WASAPI,
            DeviceId = "only-output",
            FriendlyName = "Speakers"
        };
        _mockDeviceManager.Setup(manager => manager.GetCachedAvailableDevicesAsync())
            .ReturnsAsync([asioDevice, onlyWasapiDevice]);

        var vm = CreateViewModel();
        vm.SelectModeCommand.Execute(WizardMode.Hardware);
        Dispatcher.UIThread.RunJobs();
        await vm.TryGoForwardAsync();

        await vm.ReportSameDeviceSilentCommand.ExecuteAsync(null);

        Assert.Equal(AudioSubStep.ProMixRequired, vm.CurrentAudioSubStep);
        Assert.Contains("一个播放设备", vm.ProMixRequiredMessage);
        Assert.False(vm.IsConcurrencyTestSoundPlaying);
        _mockDeviceOperations.Verify(operation => operation.DeactivateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [AvaloniaFact]
    public async Task HardwareFlow_WhenAnotherGameDeviceWorks_ContinuesToValidation()
    {
        var asioDevice = new DeviceDescription { WavePlayerType = WavePlayerType.ASIO, FriendlyName = "Studio ASIO" };
        _mockDeviceManager.Setup(manager => manager.GetCachedAvailableDevicesAsync())
            .ReturnsAsync([
                asioDevice,
                new DeviceDescription
                {
                    WavePlayerType = WavePlayerType.WASAPI,
                    DeviceId = "speakers",
                    FriendlyName = "Speakers"
                },
                new DeviceDescription
                {
                    WavePlayerType = WavePlayerType.WASAPI,
                    DeviceId = "monitor",
                    FriendlyName = "Monitor"
                }
            ]);

        var vm = CreateViewModel();
        vm.SelectModeCommand.Execute(WizardMode.Hardware);
        Dispatcher.UIThread.RunJobs();
        await vm.TryGoForwardAsync();

        await vm.ReportSameDeviceSilentCommand.ExecuteAsync(null);
        Assert.Equal(AudioSubStep.AlternativeDeviceCheck, vm.CurrentAudioSubStep);
        Assert.True(vm.IsConcurrencyTestSoundPlaying);

        vm.ConfirmAlternativeDeviceAudioCommand.Execute(null);

        Assert.Equal(AudioSubStep.Validation, vm.CurrentAudioSubStep);
        Assert.True(vm.ValidationSuccess);
        Assert.Contains("能听到声音", vm.ValidationInstruction);
        Assert.False(vm.IsConcurrencyTestSoundPlaying);
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
