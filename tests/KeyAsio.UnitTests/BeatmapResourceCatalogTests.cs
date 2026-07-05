using KeyAsio.Core.OsuAudio.Hitsounds;

namespace KeyAsio.UnitTests;

public sealed class BeatmapResourceCatalogTests
{
    [Fact]
    public void FromMappings_ResolvesLogicalNamesToOriginalPaths()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "keyasio-lazer-storage");
        var osuPath = Path.Combine(storageRoot, "files", "aa", "bb", "mapped-osu-file");
        var wavPath = Path.Combine(storageRoot, "files", "cc", "dd", "mapped-wav-file");

        var catalog = BeatmapResourceCatalog.FromMappings(
            [
                new BeatmapResource("Difficulties/Hard.osu", osuPath),
                new BeatmapResource("Samples/normal-hitnormal.wav", wavPath)
            ],
            storageRoot,
            "Hard.osu");

        Assert.True(catalog.TryResolve("Difficulties/Hard.osu", out var osuResource));
        Assert.Equal(Path.GetFullPath(osuPath), osuResource.Path);

        Assert.True(catalog.TryResolve("Hard.osu", out var osuResourceByLeafName));
        Assert.Equal(Path.GetFullPath(osuPath), osuResourceByLeafName.Path);

        Assert.True(catalog.TryResolveAudio("Samples/normal-hitnormal", out var wavResource));
        Assert.Equal(Path.GetFullPath(wavPath), wavResource.Path);

        Assert.True(catalog.TryResolveAudio("normal-hitnormal", out var wavResourceByLeafName));
        Assert.Equal(Path.GetFullPath(wavPath), wavResourceByLeafName.Path);
    }
}
