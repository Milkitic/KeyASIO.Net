using System.Text.Json;
using System.Text.Json.Serialization;
using KeyAsio.Application.Models;
using KeyAsio.Configuration.Models;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Application.Services;

[JsonSerializable(typeof(SkinListCacheFile))]
internal partial class SkinListCacheJsonContext : JsonSerializerContext
{
}

internal sealed class SkinListCache
{
    private const int CurrentVersion = 1;

    private readonly ILogger _logger;
    private readonly string _cachePath;
    private readonly object _lock = new();

    private SkinListCacheFile? _cache;

    public SkinListCache(ILogger logger, string? cachePath = null)
    {
        _logger = logger;
        _cachePath = cachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyAsio",
            "skin-lists-v1.json");
    }

    public IReadOnlyList<SkinDescription> Get(GameClientType clientType)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var entries = clientType == GameClientType.Lazer
                ? _cache!.Lazer
                : _cache!.Stable;
            entries ??= [];

            return entries
                .Where(static entry =>
                    !string.IsNullOrWhiteSpace(entry.FolderName) &&
                    !string.IsNullOrWhiteSpace(entry.Folder))
                .Select(static entry => new SkinDescription(
                    entry.FolderName,
                    entry.Folder,
                    entry.Name,
                    entry.Author))
                .DistinctBy(static skin => skin.FolderName, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public void Save(GameClientType clientType, IEnumerable<SkinDescription> skins)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var entries = skins
                .Where(static skin =>
                    !string.IsNullOrWhiteSpace(skin.FolderName) &&
                    !string.IsNullOrWhiteSpace(skin.Folder))
                .DistinctBy(static skin => skin.FolderName, StringComparer.Ordinal)
                .Select(static skin => new CachedSkinDescription
                {
                    FolderName = skin.FolderName,
                    Folder = skin.Folder,
                    Name = skin.Name,
                    Author = skin.Author
                })
                .ToList();

            if (clientType == GameClientType.Lazer)
            {
                _cache!.Lazer = entries;
            }
            else
            {
                _cache!.Stable = entries;
            }

            WriteCache();
        }
    }

    private void EnsureLoaded()
    {
        if (_cache != null)
        {
            return;
        }

        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                var loaded = JsonSerializer.Deserialize(
                    json,
                    SkinListCacheJsonContext.Default.SkinListCacheFile);
                if (loaded?.Version == CurrentVersion)
                {
                    _cache = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached skin lists from {CachePath}", _cachePath);
        }

        _cache = new SkinListCacheFile();
    }

    private void WriteCache()
    {
        var tempPath = _cachePath + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath) ?? ".");
            var json = JsonSerializer.Serialize(
                _cache!,
                SkinListCacheJsonContext.Default.SkinListCacheFile);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _cachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cached skin lists to {CachePath}", _cachePath);

            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}

internal sealed class SkinListCacheFile
{
    public int Version { get; set; } = 1;
    public List<CachedSkinDescription> Stable { get; set; } = [];
    public List<CachedSkinDescription> Lazer { get; set; } = [];
}

internal sealed class CachedSkinDescription
{
    public string FolderName { get; set; } = "";
    public string Folder { get; set; } = "";
    public string? Name { get; set; }
    public string? Author { get; set; }
}
