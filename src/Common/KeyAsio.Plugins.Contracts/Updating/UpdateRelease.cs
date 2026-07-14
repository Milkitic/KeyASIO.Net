namespace KeyAsio.Plugins.Contracts;

public sealed record UpdateAsset(
    string Name,
    string DownloadUrl,
    long? Size = null);

public sealed record UpdateRelease(
    string Version,
    string? ReleasePageUrl,
    string? Notes,
    bool IsPrerelease,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<UpdateAsset> Assets);
