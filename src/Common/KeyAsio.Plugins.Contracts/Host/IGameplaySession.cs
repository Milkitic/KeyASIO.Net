using System.Diagnostics.CodeAnalysis;
using Coosu.Beatmap;

namespace KeyAsio.Plugins.Contracts;

/// <summary>
/// Read-only view of the host gameplay session and its prepared audio resources.
/// </summary>
public interface IGameplaySession
{
    OsuFile? Beatmap { get; }

    string? BeatmapFolder { get; }

    string? AudioFilename { get; }

    event Action? SessionStopped;

    bool TryResolveResource(string name, out PluginGameplayResource resource);

    bool TryResolveAudioResource(string fileNameOrNameWithoutExtension, out PluginGameplayResource resource);

    bool TryCreateCachedAudioProvider(
        string path,
        [NotNullWhen(true)] out ISeekableAudioSampleProvider? sampleProvider);
}

public sealed record PluginGameplayResource(string Name, string Path)
{
    public Stream OpenRead() => File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
}
