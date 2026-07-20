using KeyAsio.Application.Models;
using KeyAsio.Application.Services;
using KeyAsio.Configuration.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace KeyAsio.UnitTests;

public sealed class SkinListCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"keyasio-skin-cache-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Save_RoundTripsListsPerClient()
    {
        var path = Path.Combine(_directory, "skin-lists.json");
        var cache = new SkinListCache(NullLogger.Instance, path);

        cache.Save(GameClientType.Stable,
        [
            SkinDescription.Internal,
            new SkinDescription("stable-skin", "D:\\osu!\\Skins\\stable-skin", "Stable", "Author")
        ]);
        cache.Save(GameClientType.Lazer,
        [
            SkinDescription.Internal,
            new SkinDescription("Lazer", "{lazer-skin:id}", "Lazer", null)
        ]);

        var reloaded = new SkinListCache(NullLogger.Instance, path);

        Assert.Contains(reloaded.Get(GameClientType.Stable), skin => skin.FolderName == "stable-skin");
        Assert.Contains(reloaded.Get(GameClientType.Lazer), skin => skin.Folder == "{lazer-skin:id}");
        Assert.DoesNotContain(reloaded.Get(GameClientType.Stable), skin => skin.Folder == "{lazer-skin:id}");
    }

    [Fact]
    public void Get_PreservesIdentifierUsedToRestoreSelectionAfterRestart()
    {
        var path = Path.Combine(_directory, "skin-lists.json");
        var cache = new SkinListCache(NullLogger.Instance, path);
        const string selectedSkin = "Chosen skin";
        cache.Save(GameClientType.Lazer,
        [
            new SkinDescription(selectedSkin, "{lazer-skin:chosen-id}", "Chosen skin", null)
        ]);

        var cachedSkins = new SkinListCache(NullLogger.Instance, path)
            .Get(GameClientType.Lazer);

        Assert.Contains(cachedSkins, skin => skin.FolderName == selectedSkin);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
