using System.Collections.Concurrent;

namespace KeyAsio.Core.Audio.Utils;

public static class SharedPool<T> where T : class, IPoolable, new()
{
    private static readonly ConcurrentBag<T> s_items = new();
    private const int MaxCapacity = 64;

    public static int Count => s_items.Count;

    public static T Rent()
    {
        if (s_items.TryTake(out var item))
        {
            return item;
        }

        return new T();
    }

    public static void Return(T item)
    {
        if (item.ExcludeFromPool) return;

        if (s_items.Count >= MaxCapacity)
        {
            return;
        }

        item.Reset();
        s_items.Add(item);
    }

    public static void Clear() => s_items.Clear();
}