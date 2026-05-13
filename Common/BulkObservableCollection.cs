using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ApexHMI.Common;

/// <summary>
/// 扩展 ObservableCollection&lt;T&gt;：支持一次性替换全部元素并只触发一次 Reset 通知，
/// 避免大批量 Add 时绑定层的 N 次重算引起 UI 卡死。
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    public BulkObservableCollection() : base() { }
    public BulkObservableCollection(IEnumerable<T> items) : base(items) { }

    /// <summary>用新集合一次性替换内部元素，仅触发一次 CollectionChanged(Reset)。</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var i in items) Items.Add(i);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
