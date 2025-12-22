using System.Collections.Concurrent;

namespace KeyAsio.Core.Audio.Caching;

internal class CategoryCache
{
    public ConcurrentDictionary<string, string> PathHashCaches { get; } = new();

    public ConcurrentDictionary<string, Lazy<Task<CachedAudio>>> AudioCachesByHash { get; } = new();
}