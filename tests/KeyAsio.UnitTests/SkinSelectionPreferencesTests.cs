using KeyAsio.Application.Services;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Models;

namespace KeyAsio.UnitTests;

public sealed class SkinSelectionPreferencesTests
{
    [Fact]
    public void OnSelectionChanged_StoresPreferencesPerClient()
    {
        var paths = new AppSettingsPaths();
        var preferences = new SkinSelectionPreferences(paths);

        preferences.OnSelectionChanged(GameClientType.Stable, "stable-skin");
        preferences.OnSelectionChanged(GameClientType.Lazer, "lazer-skin");

        Assert.Equal("stable-skin", preferences.Get(GameClientType.Stable));
        Assert.Equal("lazer-skin", preferences.Get(GameClientType.Lazer));
    }

    [Fact]
    public void ApplyProgrammaticSelection_DoesNotOverwriteSavedPreferenceWithFallback()
    {
        var paths = new AppSettingsPaths
        {
            SelectedSkinNameLazer = "lazer-skin"
        };
        var preferences = new SkinSelectionPreferences(paths);

        preferences.ApplyProgrammaticSelection(
            () => preferences.OnSelectionChanged(GameClientType.Lazer, "{internal}"));

        Assert.Equal("lazer-skin", preferences.Get(GameClientType.Lazer));
    }

    [Fact]
    public void ApplyProgrammaticSelection_RestoresPersistenceAfterFailure()
    {
        var paths = new AppSettingsPaths();
        var preferences = new SkinSelectionPreferences(paths);

        Assert.Throws<InvalidOperationException>(() =>
            preferences.ApplyProgrammaticSelection(
                () => throw new InvalidOperationException("test")));

        preferences.OnSelectionChanged(GameClientType.Stable, "stable-skin");
        Assert.Equal("stable-skin", preferences.Get(GameClientType.Stable));
    }
}
