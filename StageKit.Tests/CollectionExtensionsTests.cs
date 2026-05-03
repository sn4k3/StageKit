using System.Collections.ObjectModel;
using ObservableCollections;

namespace StageKit.Tests;

public sealed class CollectionExtensionsTests
{
    [Fact]
    public void RemoveExceedingAt_WhenHead_RemovesOldestItems()
    {
        var collection = new ObservableCollection<int>([1, 2, 3, 4]);

        collection.RemoveExceedingAt(2, CollectionSide.Head);

        Assert.Equal([3, 4], collection);
    }

    [Fact]
    public void RemoveExceedingAt_WhenTail_RemovesNewestItems()
    {
        var collection = new ObservableCollection<int>([1, 2, 3, 4]);

        collection.RemoveExceedingAt(2, CollectionSide.Tail);

        Assert.Equal([1, 2], collection);
    }

    [Fact]
    public void RemoveExceedingAt_WhenCountIsWithinLimit_DoesNothing()
    {
        var collection = new ObservableCollection<int>([1, 2]);

        collection.RemoveExceedingAt(2, CollectionSide.Head);

        Assert.Equal([1, 2], collection);
    }

    [Fact]
    public void RemoveExceedingAt_WhenObservableListHead_RemovesOldestItems()
    {
        var collection = new ObservableList<int>([1, 2, 3, 4]);

        collection.RemoveExceedingAt(2, CollectionSide.Head);

        Assert.Equal([3, 4], collection);
    }
}
