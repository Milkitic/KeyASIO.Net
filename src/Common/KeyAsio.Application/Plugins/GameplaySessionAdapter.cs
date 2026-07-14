using System.Diagnostics.CodeAnalysis;
using Coosu.Beatmap;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Plugins.Contracts;
using KeyAsio.Sync;
using KeyAsio.Sync.Services;

namespace KeyAsio.Application.Plugins;

public sealed class GameplaySessionAdapter : IGameplaySession, IDisposable
{
    private readonly GameplaySessionManager _session;
    private readonly SyncSessionContext _syncContext;
    private readonly AudioCacheManager _audioCache;

    public GameplaySessionAdapter(
        GameplaySessionManager session,
        SyncSessionContext syncContext,
        AudioCacheManager audioCache)
    {
        _session = session;
        _syncContext = syncContext;
        _audioCache = audioCache;
        _session.SessionStopped += OnSessionStopped;
    }

    public OsuFile? Beatmap => _session.OsuFile;
    public string? BeatmapFolder => _session.BeatmapFolder;
    public string? AudioFilename => _session.AudioFilename;
    public event Action? SessionStopped;

    public bool TryResolveResource(string name, out PluginGameplayResource resource)
    {
        resource = null!;
        if (_syncContext.BeatmapResourceCatalog?.TryResolve(name, out var resolved) != true)
        {
            return false;
        }

        resource = new PluginGameplayResource(resolved.Name, resolved.Path);
        return true;
    }

    public bool TryResolveAudioResource(string fileNameOrNameWithoutExtension, out PluginGameplayResource resource)
    {
        resource = null!;
        if (_syncContext.BeatmapResourceCatalog?.TryResolveAudio(fileNameOrNameWithoutExtension, out var resolved) != true)
        {
            return false;
        }

        resource = new PluginGameplayResource(resolved.Name, resolved.Path);
        return true;
    }

    public bool TryCreateCachedAudioProvider(
        string path,
        [NotNullWhen(true)] out ISeekableAudioSampleProvider? sampleProvider)
    {
        sampleProvider = null;
        if (!_audioCache.TryGet(path, out var cachedAudio))
        {
            return false;
        }

        sampleProvider = new CachedPluginAudioProvider(path, cachedAudio);
        return true;
    }

    public void Dispose() => _session.SessionStopped -= OnSessionStopped;

    private void OnSessionStopped() => SessionStopped?.Invoke();

    private sealed class CachedPluginAudioProvider : ISeekableAudioSampleProvider
    {
        private readonly CachedAudioProvider _provider;

        public CachedPluginAudioProvider(string path, CachedAudio cachedAudio)
        {
            ResourceId = cachedAudio.SourceHash ?? Path.GetFullPath(path);
            _provider = new CachedAudioProvider(cachedAudio) { ExcludeFromPool = true };
        }

        public string ResourceId { get; }
        public NAudio.Wave.WaveFormat WaveFormat => _provider.WaveFormat;
        public TimeSpan Position { get => _provider.PlayTime; set => _provider.PlayTime = value; }

        public int Read(float[] buffer, int offset, int count) => _provider.Read(buffer, offset, count);
    }
}
