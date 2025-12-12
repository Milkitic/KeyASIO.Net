using System.Collections.Concurrent;

namespace KeyAsio.Audio.Utils;

public static class SharedPool<T> where T : class, IPoolable, new()
{
    private static readonly ConcurrentBag<T> Items = new();
    private const int MaxCapacity = 64;

    public static int Count => Items.Count;

    public static T Rent()
    {
        if (Items.TryTake(out var item))
        {
            return item;
        }

        return new T();
    }

    public static void Return(T item)
    {
        if (item.ExcludeFromPool) return;

        if (Items.Count >= MaxCapacity)
        {
            return;
        }

        item.Reset();
        Items.Add(item);
    }

    public static void Clear() => Items.Clear();
}