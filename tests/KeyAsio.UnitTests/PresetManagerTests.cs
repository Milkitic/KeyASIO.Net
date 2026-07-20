using KeyAsio.Configuration;
using KeyAsio.Services;

namespace KeyAsio.UnitTests;

public class PresetManagerTests
{
    [Fact]
    public async Task GetCurrentPresetMode_ReturnsFast_AfterApplyingFastPreset()
    {
        var settings = new AppSettings();
        var manager = new PresetManager(settings);

        await manager.ApplyPreset(PresetMode.Fast, null!);

        Assert.Equal(PresetMode.Fast, manager.GetCurrentPresetMode());
    }

    [Fact]
    public async Task GetCurrentPresetMode_ReturnsExtreme_AfterApplyingExtremePreset()
    {
        var settings = new AppSettings();
        var manager = new PresetManager(settings);

        await manager.ApplyPreset(PresetMode.Extreme, null!);

        Assert.Equal(PresetMode.Extreme, manager.GetCurrentPresetMode());
    }
}
