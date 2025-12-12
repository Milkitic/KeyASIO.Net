namespace KeyAsio.Shared.Utils;

public static class MathEx
{
    public static T Max<T>(params T[] values) where T : IComparable
    {
        return Max(values.AsEnumerable());
    }

    public static T Max<T>(IEnumerable<T> values) where T : IComparable
    {
        var def = default(T);

        foreach (var value in values)
        {
            if (def == null || def.CompareTo(value) < 0) def = value;
        }

        return def;
    }
}