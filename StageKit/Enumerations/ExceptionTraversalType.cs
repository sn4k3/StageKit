namespace StageKit;

/// <summary>
/// Defines how an exception and its inner exceptions are traversed.
/// </summary>
public enum ExceptionTraversalType
{
    /// <summary>
    /// Traverses the complete exception tree in depth-first pre-order, expanding all
    /// <see cref="AggregateException.InnerExceptions"/> collections.
    /// </summary>
    ExceptionTree,

    /// <summary>
    /// Traverses only the direct <see cref="Exception.InnerException"/> chain without expanding
    /// <see cref="AggregateException.InnerExceptions"/> collections.
    /// </summary>
    InnerExceptionChain
}
