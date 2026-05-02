using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace StageKit;

/// <summary>
/// Base class for root settings files that persist an observable collection.
/// </summary>
/// <typeparam name="T">The concrete settings file type.</typeparam>
/// <typeparam name="TO">The item type stored in the collection.</typeparam>
public abstract class RootCollectionFile<T, TO> : RootSettingsFile<T>, IList<TO> where T : RootCollectionFile<T, TO>, new()
{
    /// <summary>
    /// Gets the collection of items contained in the list.
    /// </summary>
    /// <remarks>
    /// Collection changes trigger debounced saves when saving is enabled.
    /// </remarks>
    public ObservableCollection<TO> Items { get; } = [];

    /// <summary>
    /// Gets the maximum number of items retained when saving.
    /// </summary>
    /// <remarks>Use 0 to disable trimming.</remarks>
    [JsonIgnore]
    public virtual int TrimCollectionWhenExceeding => 0;

    /// <summary>
    /// Gets the side of the collection trimmed when <see cref="TrimCollectionWhenExceeding"/> is exceeded.
    /// </summary>
    [JsonIgnore]
    public virtual CollectionSide TrimCollectionSide => CollectionSide.Head;

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AutoSave))
        {
            if (AutoSave)
            {
                Items.CollectionChanged += ItemsOnCollectionChanged;
            }
            else
            {
                Items.CollectionChanged -= ItemsOnCollectionChanged;
            }
        }
        base.OnPropertyChanged(e);
    }

    /// <summary>
    /// Handles collection changes by triggering a debounced save if AutoSave is enabled.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!AutoSave || !IsLoaded) return;
        Debug.WriteLine($"[{GetType().Name}] Collection changed: {e.Action}, NewItems: {e.NewItems?.Count ?? 0}, OldItems: {e.OldItems?.Count ?? 0}");
        DebouncedSave(DefaultDebounceSaveMilliseconds);
    }

    /// <inheritdoc />
    protected override void BeforeSave()
    {
        base.BeforeSave();
        if (TrimCollectionWhenExceeding <= 0) return;
        if (AutoSave)
        {
            Items.CollectionChanged -= ItemsOnCollectionChanged;
            try
            {
                Items.RemoveExceedingAt(TrimCollectionWhenExceeding, TrimCollectionSide);
            }
            finally
            {
                Items.CollectionChanged += ItemsOnCollectionChanged;
            }
        }
        else
        {
            Items.RemoveExceedingAt(TrimCollectionWhenExceeding, TrimCollectionSide);
        }
    }

    #region IList
    /// <inheritdoc />
    public IEnumerator<TO> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Items).GetEnumerator();
    }

    /// <inheritdoc />
    public void Add(TO item)
    {
        Items.Add(item);
    }

    /// <inheritdoc />
    public void Clear()
    {
        Items.Clear();
    }

    /// <inheritdoc />
    public bool Contains(TO item)
    {
        return Items.Contains(item);
    }

    /// <inheritdoc />
    public void CopyTo(TO[] array, int arrayIndex)
    {
        Items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public bool Remove(TO item)
    {
        return Items.Remove(item);
    }

    /// <inheritdoc />
    public int Count => Items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public int IndexOf(TO item)
    {
        return Items.IndexOf(item);
    }

    /// <inheritdoc />
    public void Insert(int index, TO item)
    {
        Items.Insert(index, item);
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        Items.RemoveAt(index);
    }

    /// <inheritdoc />
    public TO this[int index]
    {
        get => Items[index];
        set => Items[index] = value;
    }
    #endregion

    #region Dispose

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (disposing)
        {
            Items.CollectionChanged -= ItemsOnCollectionChanged;
        }
        base.Dispose(disposing);
    }

    #endregion

}
