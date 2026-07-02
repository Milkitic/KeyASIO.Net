using System.Collections.Concurrent;

namespace KeyAsio.Core.OsuAudio.Hitsounds;

public sealed record BeatmapResource(string Name, string Path)
{
    public string CacheKey => Path;

    public Stream OpenRead()
        => File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
}

public interface IBeatmapResourceCatalog
{
    string? RootPath { get; }
    string CacheKey { get; }
    IReadOnlyList<BeatmapResource> Resources { get; }
    bool IsEmpty { get; }
    bool TryResolve(string name, out BeatmapResource resource);
    bool TryResolveAudio(string fileNameOrNameWithoutExtension, out BeatmapResource resource);
}

public sealed class BeatmapResourceCatalog : IBeatmapResourceCatalog
{
    private readonly Dictionary<string, BeatmapResource> _filesByName;
    private readonly Dictionary<string, BeatmapResource> _filesByLeafName;
    private readonly ConcurrentDictionary<string, BeatmapResource?> _audioLookupCache = new(StringComparer.OrdinalIgnoreCase);

    public BeatmapResourceCatalog(IEnumerable<BeatmapResource> resources, string? rootPath = null, string? cacheKey = null)
    {
        RootPath = string.IsNullOrWhiteSpace(rootPath) ? null : System.IO.Path.GetFullPath(rootPath);
        CacheKey = cacheKey ?? RootPath ?? string.Empty;

        var validFiles = resources
            .Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Path))
            .Select(f => new BeatmapResource(NormalizeName(f.Name), System.IO.Path.GetFullPath(f.Path)))
            .ToArray();

        Resources = validFiles;
        _filesByName = new Dictionary<string, BeatmapResource>(StringComparer.OrdinalIgnoreCase);
        _filesByLeafName = new Dictionary<string, BeatmapResource>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in validFiles)
        {
            _filesByName.TryAdd(file.Name, file);
            _filesByLeafName.TryAdd(System.IO.Path.GetFileName(file.Name), file);
        }
    }

    public string? RootPath { get; }
    public string CacheKey { get; }
    public IReadOnlyList<BeatmapResource> Resources { get; }
    public bool IsEmpty => Resources.Count == 0;

    public static BeatmapResourceCatalog FromDirectory(string directory)
    {
        var fullDirectory = System.IO.Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory))
        {
            return new BeatmapResourceCatalog([], fullDirectory);
        }

        return new BeatmapResourceCatalog(
            Directory.EnumerateFiles(fullDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new BeatmapResource(System.IO.Path.GetFileName(path), path)),
            fullDirectory);
    }

    public static BeatmapResourceCatalog FromMappings(IEnumerable<BeatmapResource> resources, string? rootPath = null,
        string? cacheKey = null)
        => new(resources, rootPath, cacheKey);

    public bool TryResolve(string name, out BeatmapResource resource)
    {
        resource = null!;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalizedName = NormalizeName(name);
        if (_filesByName.TryGetValue(normalizedName, out var foundFile))
        {
            resource = foundFile;
            return true;
        }

        if (_filesByLeafName.TryGetValue(System.IO.Path.GetFileName(normalizedName), out foundFile))
        {
            resource = foundFile;
            return true;
        }

        return false;
    }

    public bool TryResolveAudio(string fileNameOrNameWithoutExtension, out BeatmapResource resource)
    {
        resource = null!;

        if (string.IsNullOrWhiteSpace(fileNameOrNameWithoutExtension))
        {
            return false;
        }

        var normalizedName = NormalizeName(fileNameOrNameWithoutExtension);
        if (_audioLookupCache.TryGetValue(normalizedName, out var cached))
        {
            if (cached == null)
            {
                return false;
            }

            resource = cached;
            return true;
        }

        if (!string.IsNullOrEmpty(System.IO.Path.GetExtension(normalizedName)) &&
            TryResolve(normalizedName, out resource))
        {
            _audioLookupCache.TryAdd(normalizedName, resource);
            return true;
        }

        var nameWithoutExtension = RemoveExtension(normalizedName);
        foreach (var extension in KeyAsio.Core.OsuAudio.Utils.OsuAudioFileCache.SupportExtensionsInPriorityOrder)
        {
            if (TryResolve(nameWithoutExtension + extension, out resource))
            {
                _audioLookupCache.TryAdd(normalizedName, resource);
                return true;
            }
        }

        _audioLookupCache.TryAdd(normalizedName, null);
        return false;
    }

    public static string NormalizeName(string name)
        => name.Replace('\\', '/').TrimStart('/');

    public static string RemoveExtension(string name)
    {
        var directory = System.IO.Path.GetDirectoryName(name);
        var filename = System.IO.Path.GetFileNameWithoutExtension(name);

        return string.IsNullOrEmpty(directory)
            ? filename
            : NormalizeName(System.IO.Path.Combine(directory, filename));
    }
}
