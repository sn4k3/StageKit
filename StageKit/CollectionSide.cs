using System.Collections.ObjectModel;

namespace StageKit;

/// <summary>
/// Identifies which side of a collection should be trimmed.
/// </summary>
public enum CollectionSide
{
    /// <summary>
    /// Trim items from the beginning of the collection.
    /// </summary>
    Head,

    /// <summary>
    /// Trim items from the end of the collection.
    /// </summary>
    Tail
}

/// <summary>
/// Provides collection helper methods used by StageKit settings files.
/// </summary>
public static class CollectionExtensions
{
    extension<T>(ObservableCollection<T> collection)
    {
        /// <summary>
        /// Removes excess items so the collection contains at most <paramref name="maxItemCount"/> items.
        /// </summary>
        /// <param name="maxItemCount">The maximum number of items to keep.</param>
        /// <param name="side">The side from which excess items are removed.</param>
        /// <exception cref="ArgumentNullException">Thrown when the collection is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maxItemCount"/> is zero or negative, or when <paramref name="side"/> is invalid.
        /// </exception>
        public void RemoveExceedingAt(int maxItemCount, CollectionSide side)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItemCount);

            var exceed = collection.Count - maxItemCount;
            if (exceed <= 0)
            {
                return;
            }

            switch (side)
            {
                case CollectionSide.Head:
                    for (var i = 0; i < exceed; i++)
                    {
                        collection.RemoveAt(0);
                    }
                    break;
                case CollectionSide.Tail:
                    for (var i = 0; i < exceed; i++)
                    {
                        collection.RemoveAt(maxItemCount);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }
    }
}
