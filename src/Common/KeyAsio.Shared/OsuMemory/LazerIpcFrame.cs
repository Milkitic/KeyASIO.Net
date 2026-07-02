using KeyAsio.Plugins.Abstractions;

namespace KeyAsio.Shared.OsuMemory;

public sealed class LazerIpcFrame
{
    public int Version { get; init; }
    public int ProcessId { get; init; }
    public int Status { get; init; }
    public int PlayTime { get; init; }
    public uint Mods { get; init; }
    public int Combo { get; init; }
    public int Score { get; init; }
    public bool IsReplay { get; init; }
    public string? Username { get; init; }
    public string? BeatmapFolder { get; init; }
    public string? BeatmapFilename { get; init; }
    public LazerIpcFile[] BeatmapFiles { get; init; } = [];
    public SyncStatistics Statistics { get; init; }
    public int HitErrorIndex { get; init; }
    public int[] HitErrors { get; init; } = [];
}

public sealed class LazerIpcFile
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
