using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

    /// <summary>
    /// Initializes a new root collection file and subscribes to collection changes.
    /// </summary>
    protected RootCollectionFile()
    {
        Items.CollectionChanged += ItemsOnCollectionChanged;
    }

    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DebouncedSave(DefaultDebounceSaveMilliseconds);
    }

    /// <inheritdoc />
    protected override void BeforeSave()
    {
        base.BeforeSave();
        if (TrimCollectionWhenExceeding > 0)
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
    }

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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Items.CollectionChanged -= ItemsOnCollectionChanged;
        }
        base.Dispose(disposing);
    }
}
