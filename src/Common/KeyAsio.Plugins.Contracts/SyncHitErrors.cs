namespace KeyAsio.Plugins.Contracts;

public readonly record struct SyncHitErrors(int Index, int[] Values)
{
    public static SyncHitErrors Empty { get; } = new(0, []);
}
