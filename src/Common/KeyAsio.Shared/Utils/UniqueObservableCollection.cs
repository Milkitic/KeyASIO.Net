namespace KeyAsio.Shared.Utils;

public class UniqueObservableCollection<T> : ObservableRangeCollection<T>
{
    private readonly HashSet<T> _itemSet;
    private readonly IEqualityComparer<T> _comparer;

    public UniqueObservableCollection(IEqualityComparer<T>? comparer = null)
    {
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _itemSet = new HashSet<T>(_comparer);
    }

    public UniqueObservableCollection(IEnumerable<T> collection, IEqualityComparer<T>? comparer = null)
        : base(collection.Distinct(comparer ?? EqualityComparer<T>.Default))
    {
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _itemSet = new HashSet<T>(Items, _comparer);
    }

    public bool ContainsFast(T item)
    {
        return _itemSet.Contains(item);
    }

    protected override void InsertItem(int index, T item)
    {
        if (_itemSet.Add(item))
        {
            base.InsertItem(index, item);
        }
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = Items[index];

        if (_comparer.Equals(oldItem, item))
            return;

        if (_itemSet.Contains(item))
            return;

        _itemSet.Remove(oldItem);
        _itemSet.Add(item);
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        _itemSet.Remove(Items[index]);
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        _itemSet.Clear();
        base.ClearItems();
    }
}