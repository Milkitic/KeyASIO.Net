using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace KeyAsio.Shared.Utils;

public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    #region Constructors

    public ObservableRangeCollection()
    {
    }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }

    #endregion

    #region AddRange

    /// <summary>
    /// 添加多个元素到集合末尾
    /// </summary>
    /// <param name="collection">要添加的元素集合</param>
    /// <param name="notificationMode">通知模式: Add(逐项通知) 或 Reset(重置通知)</param>
    public void AddRange(IEnumerable<T> collection, NotifyCollectionChangedAction notificationMode = NotifyCollectionChangedAction.Add)
    {
        InsertRange(Count, collection, notificationMode);
    }

    #endregion

    #region InsertRange

    /// <summary>
    /// 在指定位置插入多个元素
    /// </summary>
    public void InsertRange(int index, IEnumerable<T> collection, NotifyCollectionChangedAction notificationMode = NotifyCollectionChangedAction.Add)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (notificationMode != NotifyCollectionChangedAction.Add && notificationMode != NotifyCollectionChangedAction.Reset)
            throw new ArgumentException("Mode must be either Add or Reset.", nameof(notificationMode));

        // 提前物化集合,避免多次枚举
        var items = collection as IList<T> ?? collection.ToList();
        if (items.Count == 0) return;

        CheckReentrancy();

        // 执行插入
        var itemsList = (List<T>)Items;
        itemsList.InsertRange(index, items);

        // 发送通知
        if (notificationMode == NotifyCollectionChangedAction.Reset)
        {
            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
        }
        else
        {
            RaiseChangeNotificationEvents(
                NotifyCollectionChangedAction.Add,
                items as List<T> ?? new List<T>(items),
                index);
        }
    }

    #endregion

    #region RemoveRange

    /// <summary>
    /// 移除指定元素集合中的每个元素(第一次出现)
    /// </summary>
    public void RemoveRange(IEnumerable<T> collection, NotifyCollectionChangedAction notificationMode = NotifyCollectionChangedAction.Reset)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));
        if (notificationMode != NotifyCollectionChangedAction.Remove && notificationMode != NotifyCollectionChangedAction.Reset)
            throw new ArgumentException("Mode must be either Remove or Reset.", nameof(notificationMode));

        if (Count == 0) return;

        var items = collection as IList<T> ?? collection.ToList();
        if (items.Count == 0) return;

        CheckReentrancy();

        if (notificationMode == NotifyCollectionChangedAction.Reset)
        {
            var removed = false;
            foreach (var item in items)
            {
                if (Items.Remove(item))
                    removed = true;
            }

            if (removed)
            {
                RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
            }

            return;
        }

        // Remove 模式: 只移除实际存在的元素
        var removedItems = new List<T>();
        foreach (var item in items)
        {
            if (Items.Remove(item))
                removedItems.Add(item);
        }

        if (removedItems.Count > 0)
        {
            RaiseChangeNotificationEvents(
                NotifyCollectionChangedAction.Remove,
                removedItems);
        }
    }

    /// <summary>
    /// 移除指定范围的元素
    /// </summary>
    public void RemoveRange(int index, int count)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (index + count > Count) throw new ArgumentOutOfRangeException(nameof(index));

        if (count == 0) return;

        if (count == 1)
        {
            RemoveItem(index);
            return;
        }

        CheckReentrancy();

        var items = (List<T>)Items;
        var removedItems = items.GetRange(index, count);
        items.RemoveRange(index, count);

        RaiseChangeNotificationEvents(
            NotifyCollectionChangedAction.Remove,
            removedItems,
            index);
    }

    #endregion

    #region Replace

    /// <summary>
    /// 清空集合并替换为单个元素
    /// </summary>
    public void Replace(T item) => ReplaceRange([item]);

    /// <summary>
    /// 清空集合并替换为指定集合
    /// </summary>
    public void ReplaceRange(IEnumerable<T> collection)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        var items = collection as IList<T> ?? collection.ToList();

        if (Count == 0 && items.Count == 0) return;

        CheckReentrancy();

        Items.Clear();

        if (items.Count > 0)
        {
            var itemsList = (List<T>)Items;
            itemsList.AddRange(items);
        }

        RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
    }

    #endregion

    #region RemoveAll (Bonus)

    /// <summary>
    /// 移除所有满足条件的元素
    /// </summary>
    /// <returns>移除的元素数量</returns>
    public int RemoveAll(Predicate<T> match)
    {
        if (match == null) throw new ArgumentNullException(nameof(match));
        if (Count == 0) return 0;

        CheckReentrancy();

        var removedItems = new List<T>();

        // 从后向前遍历,避免索引问题
        for (int i = Count - 1; i >= 0; i--)
        {
            if (match(Items[i]))
            {
                removedItems.Insert(0, Items[i]);
                Items.RemoveAt(i);
            }
        }

        if (removedItems.Count > 0)
        {
            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
        }

        return removedItems.Count;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 触发属性和集合变更通知
    /// </summary>
    private void RaiseChangeNotificationEvents(
        NotifyCollectionChangedAction action,
        List<T>? changedItems = null,
        int startingIndex = -1)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

        if (changedItems == null)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action));
        }
        else
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                action,
                changedItems: changedItems,
                startingIndex: startingIndex));
        }
    }

    #endregion
}