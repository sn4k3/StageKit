using System.Collections;
using System.ComponentModel;
using System.Text.Json.Serialization;
using ObservableCollections;

namespace StageKit;

/// <summary>
/// Base class for root settings files that persist an observable collection.
/// </summary>
/// <typeparam name="T">The concrete settings file type.</typeparam>
/// <typeparam name="TO">The item type stored in the collection.</typeparam>
public abstract class RootCollectionFile<T, TO> : RootSettingsFile<T>, IList<TO> where T : RootCollectionFile<T, TO>, new()
{
    #region Properties
    /// <summary>
    /// Gets the collection of items contained in the list.
    /// </summary>
    /// <remarks>
    /// Collection changes trigger debounced saves when saving is enabled.
    /// </remarks>
    public ObservableList<TO> Items { get; } = [];

    /// <summary>
    /// Gets a thread-safe view of the items collection that can be safely bound to from any thread without needing to marshal to the UI thread.
    /// </summary>
    /// <remarks>
    /// The view captures <see cref="SynchronizationContext.Current"/> at construction time. To bind to a UI thread,
    /// ensure the UI <see cref="SynchronizationContext"/> is installed on the calling thread before the settings
    /// instance is first accessed (e.g., via <c>Instance</c>).
    /// </remarks>
    public NotifyCollectionChangedSynchronizedViewList<TO> ItemsView { get; }

    /// <summary>
    /// Gets the maximum number of items retained when saving.
    /// </summary>
    /// <remarks>Use 0 to disable trimming.</remarks>
    [JsonIgnore]
    public int TrimCollectionWhenExceeding { get; set; }

    /// <summary>
    /// Gets the side of the collection trimmed when <see cref="TrimCollectionWhenExceeding"/> is exceeded.
    /// </summary>
    [JsonIgnore]
    public CollectionSide TrimCollectionSide { get; init; } = CollectionSide.Head;

    /// <summary>
    /// Gets a value indicating whether to track items that implement <see cref="INotifyPropertyChanged"/> for changes, and trigger saves when they change. If <see langword="false"/>, only changes to the collection itself will trigger saves.
    /// </summary>
    /// <remarks>Use <see langword="false"/> when items are immutable. Also keep in mind this can be heavy with a lot of items in the collection.</remarks>
    [JsonIgnore]
    protected bool TrackItemsWithChangeNotification { get; init; } = false;
    #endregion

    #region Constructor
    /// <inheritdoc />
    protected RootCollectionFile()
    {
        ItemsView = SynchronizationContext.Current is { } synchronizationContext
            ? Items.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(synchronizationContext))
            : Items.ToNotifyCollectionChanged();
        Items.CollectionChanged += ItemsOnCollectionChanged;
    }
    #endregion

    #region Events
    private void ItemsOnCollectionChanged(in NotifyCollectionChangedEventArgs<TO> e)
    {
        if (TrackItemsWithChangeNotification)
        {
            foreach (var item in e.OldItems)
            {
                if (item is INotifyPropertyChanged notify)
                {
                    notify.PropertyChanged -= TrackItemsOnPropertyChanged;
                }
            }

            foreach (var item in e.NewItems)
            {
                if (item is INotifyPropertyChanged notify)
                {
                    notify.PropertyChanged += TrackItemsOnPropertyChanged;
                }
            }
        }

        if (!IsLoaded) return;
        HasUnsavedChanges = true;
        if (AutoSave) DebouncedSave(DefaultDebounceSaveMilliseconds);
    }

    private void TrackItemsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded) return;
        HasUnsavedChanges = true;
        if (AutoSave) DebouncedSave(DefaultDebounceSaveMilliseconds);
    }

    /// <inheritdoc />
    protected override void BeforeSave()
    {
        base.BeforeSave();
        var maxItemCount = TrimCollectionWhenExceeding;
        if (maxItemCount <= 0 || Items.Count <= maxItemCount) return;
        Items.RemoveExceedingAt(maxItemCount, TrimCollectionSide);
    }
    #endregion

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
            ItemsView.Dispose();

            foreach (var item in Items)
            {
                if (item is INotifyPropertyChanged notify)
                {
                    notify.PropertyChanged -= TrackItemsOnPropertyChanged;
                }
            }
        }
        base.Dispose(disposing);
    }

    #endregion

}
