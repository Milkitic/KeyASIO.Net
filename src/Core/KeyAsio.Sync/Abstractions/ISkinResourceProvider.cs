using KeyAsio.Core.OsuAudio.Hitsounds;

namespace KeyAsio.Sync.Abstractions;

public interface ISkinResourceProvider
{
    event Action? ResourcesChanged;

    bool TryGetSkinCatalog(string skinFolder, out IBeatmapResourceCatalog catalog);

    bool TryGetLazerResource(string skinFolder, string key, out byte[] data);

    bool TryGetStableResource(string key, out byte[] data);
}
