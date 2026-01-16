using System.Collections.Concurrent;
using KeyAsio.Shared.Hitsounds.Playback;

namespace KeyAsio.Shared.Utils;

public class OsuAudioFileCache
{
    public const string WavExtension = ".wav";
    public const string OggExtension = ".ogg";
    public const string Mp3Extension = ".mp3";

    private readonly ConcurrentDictionary<string, (string filename, ResourceOwner resourceOwner)> _pathCache = new();

    private static readonly string[] SupportExtensionsInPriorityOrder = [WavExtension, Mp3Extension, OggExtension];

    public static IReadOnlySet<string> SupportExtensions { get; } =
        new HashSet<string>(SupportExtensionsInPriorityOrder, StringComparer.OrdinalIgnoreCase);

    public string GetFileUntilFind(string sourceFolder, string fileNameWithoutExtension, out ResourceOwner resourceOwner)
    {
        var combine = Path.Combine(sourceFolder, fileNameWithoutExtension);
        if (_pathCache.TryGetValue(combine, out var tuple))
        {
            resourceOwner = tuple.resourceOwner;
            return tuple.filename;
        }

        string name = "";
        foreach (var extension in SupportExtensionsInPriorityOrder)
        {
            name = fileNameWithoutExtension + extension;
            var path = Path.Combine(sourceFolder, name);

            if (File.Exists(path))
            {
                _pathCache.TryAdd(combine, (name, ResourceOwner.Beatmap));
                resourceOwner = ResourceOwner.Beatmap;
                return name;
            }
        }

        _pathCache.TryAdd(combine, (name, ResourceOwner.UserSkin));
        resourceOwner = ResourceOwner.UserSkin;
        return name;
    }
}