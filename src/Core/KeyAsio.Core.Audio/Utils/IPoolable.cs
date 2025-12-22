namespace KeyAsio.Core.Audio.Utils;

public interface IPoolable
{
    void Reset();
    bool ExcludeFromPool { get; init; }
}