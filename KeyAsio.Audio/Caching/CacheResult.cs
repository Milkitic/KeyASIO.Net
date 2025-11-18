namespace KeyAsio.Audio.Caching;

public readonly record struct CacheResult(CachedAudio? CachedAudio, CacheGetStatus Status)
{
    public static CacheResult Failed => new CacheResult(null, CacheGetStatus.Failed);
}