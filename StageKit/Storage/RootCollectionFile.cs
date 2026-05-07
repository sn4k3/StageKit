using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using ObservableCollections;
using StageKit.Extensions;

namespace StageKit;

/// <summary>
/// Base class for root settings files that persist an observable collection.
/// </summary>
/// <typeparam name="T">The concrete settings file type.</typeparam>
/// <typeparam name="TO">The item type stored in the collection.</typeparam>
public abstract class RootCollectionFile<T, TO> : RootSettingsFile<T>, IList<TO> where T : RootCollectionFile<T, TO>, new()
{
    #region Members
    private readonly Dictionary<INotifyPropertyChanged, int> _trackedItemReferenceCounts = new(ReferenceEqualityComparer.Instance);
    #endregion

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
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    TrackNewItems(e);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    UntrackOldItems(e);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    UntrackOldItems(e);
                    TrackNewItems(e);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    RebuildTrackedItems();
                    break;
            }
        }

        if (!IsLoaded) return;
        HasUnsavedChanges = true;
        ScheduleAutoSaveAfterChange(DefaultDebounceSaveMilliseconds);
    }

    private void TrackItemsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded) return;
        HasUnsavedChanges = true;
        ScheduleAutoSaveAfterChange(DefaultDebounceSaveMilliseconds);
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

    #region Methods

    /// <summary>
    /// Adds tracking for new items added to the collection, as described by the specified event arguments. This ensures that property change notifications are handled for any new items that implement <see cref="INotifyPropertyChanged"/>, allowing changes to those items to trigger saves when <see cref="TrackItemsWithChangeNotification"/> is enabled.
    /// </summary>
    /// <param name="e">The event data containing information about the items that were added to the collection. Must not be null.</param>
    private void TrackNewItems(in NotifyCollectionChangedEventArgs<TO> e)
    {
        if (e.IsSingleItem)
        {
            TrackItem(e.NewItem);
            return;
        }

        foreach (var item in e.NewItems)
        {
            TrackItem(item);
        }
    }

    /// <summary>
    /// Removes tracking for items that have been removed from the collection, as described by the specified event
    /// arguments.
    /// </summary>
    /// <param name="e">The event data containing information about the items that were removed from the collection. Must not be null.</param>
    private void UntrackOldItems(in NotifyCollectionChangedEventArgs<TO> e)
    {
        if (e.IsSingleItem)
        {
            UntrackItem(e.OldItem);
            return;
        }

        foreach (var item in e.OldItems)
        {
            UntrackItem(item);
        }
    }

    /// <summary>
    /// Tracks the specified item for property change notifications if it implements the INotifyPropertyChanged
    /// interface.
    /// </summary>
    /// <remarks>If the item is already being tracked, its reference count is incremented. This method ensures
    /// that property change events are handled only for items that support notification.</remarks>
    /// <param name="item">The item to be tracked for property changes. If the item implements INotifyPropertyChanged, it will be monitored
    /// for changes; otherwise, no action is taken.</param>
    private void TrackItem(TO item)
    {
        if (item is not INotifyPropertyChanged notify) return;

        if (_trackedItemReferenceCounts.TryGetValue(notify, out var referenceCount))
        {
            _trackedItemReferenceCounts[notify] = referenceCount + 1;
            return;
        }

        _trackedItemReferenceCounts.Add(notify, 1);
        notify.PropertyChanged += TrackItemsOnPropertyChanged;
    }

    /// <summary>
    /// Stops tracking property changes for the specified item if it is no longer referenced.
    /// </summary>
    /// <remarks>If the item is referenced multiple times, only the reference count is decremented. Property
    /// change tracking is removed only when the last reference is released.</remarks>
    /// <param name="item">The item to stop tracking. Must implement <see cref="INotifyPropertyChanged"/> to be untracked.</param>
    private void UntrackItem(TO item)
    {
        if (item is not INotifyPropertyChanged notify) return;
        if (!_trackedItemReferenceCounts.TryGetValue(notify, out var referenceCount)) return;

        if (referenceCount > 1)
        {
            _trackedItemReferenceCounts[notify] = referenceCount - 1;
            return;
        }

        _trackedItemReferenceCounts.Remove(notify);
        notify.PropertyChanged -= TrackItemsOnPropertyChanged;
    }

    /// <summary>
    /// Rebuilds the tracked items based on the current collection contents. This is used to reset tracking after a <see cref="NotifyCollectionChangedAction.Reset"/> action, which doesn't provide the old items that were removed.
    /// </summary>
    private void RebuildTrackedItems()
    {
        foreach (var item in _trackedItemReferenceCounts.Keys)
        {
            item.PropertyChanged -= TrackItemsOnPropertyChanged;
        }

        _trackedItemReferenceCounts.Clear();
        foreach (var item in Items)
        {
            TrackItem(item);
        }
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

            foreach (var item in _trackedItemReferenceCounts.Keys)
            {
                item.PropertyChanged -= TrackItemsOnPropertyChanged;
            }
            _trackedItemReferenceCounts.Clear();
        }
        base.Dispose(disposing);
    }

    #endregion

}
