namespace KeyAsio.Audio.Utils;

public interface IPoolable
{
    void Reset();
    bool ExcludeFromPool { get; init; }
}