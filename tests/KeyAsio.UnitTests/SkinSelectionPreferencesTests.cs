using KeyAsio.Application.Models;
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

        preferences.OnSelectionChanged(
            GameClientType.Stable,
            new SkinDescription("stable-skin", "D:\\osu!\\Skins\\stable-skin", null, null));
        preferences.OnSelectionChanged(
            GameClientType.Lazer,
            new SkinDescription("lazer-skin", "{lazer-skin:id}", null, null));

        Assert.Equal("stable-skin", preferences.Get(GameClientType.Stable));
        Assert.Equal("{lazer-skin:id}", preferences.Get(GameClientType.Lazer));
    }

    [Fact]
    public void ApplyProgrammaticSelection_DoesNotOverwriteSavedPreferenceWithFallback()
    {
        var paths = new AppSettingsPaths
        {
            SelectedSkinNameLazer = "{lazer-skin:id}"
        };
        var preferences = new SkinSelectionPreferences(paths);

        preferences.ApplyProgrammaticSelection(
            () => preferences.OnSelectionChanged(GameClientType.Lazer, SkinDescription.Internal));

        Assert.Equal("{lazer-skin:id}", preferences.Get(GameClientType.Lazer));
    }

    [Fact]
    public void ApplyProgrammaticSelection_RestoresPersistenceAfterFailure()
    {
        var paths = new AppSettingsPaths();
        var preferences = new SkinSelectionPreferences(paths);

        Assert.Throws<InvalidOperationException>(() =>
            preferences.ApplyProgrammaticSelection(
                () => throw new InvalidOperationException("test")));

        preferences.OnSelectionChanged(
            GameClientType.Stable,
            new SkinDescription("stable-skin", "D:\\osu!\\Skins\\stable-skin", null, null));
        Assert.Equal("stable-skin", preferences.Get(GameClientType.Stable));
    }
}
