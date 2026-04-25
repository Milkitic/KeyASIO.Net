namespace KeyAsio.Plugins.Abstractions;

public readonly record struct SyncHitErrors(int Index, int[] Values)
{
    public static SyncHitErrors Empty { get; } = new(0, []);
}
