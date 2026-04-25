namespace KeyAsio.Plugins.Abstractions;

public readonly record struct SyncStatistics(
    int Perfect,
    int Great,
    int Good,
    int Ok,
    int Meh,
    int Miss)
{
    public static SyncStatistics Empty { get; } = new();
}
