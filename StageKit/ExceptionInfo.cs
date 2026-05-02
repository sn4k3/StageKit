using System.Diagnostics.CodeAnalysis;

namespace StageKit;

/// <summary>
/// Stores serializable exception details, including optional linked inner exception information.
/// </summary>
public record ExceptionInfo
{
    /// <summary>
    /// Gets the full exception type name.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception message.
    /// </summary>
    public required string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception source.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the exception stack trace.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets the next exception in the linked exception chain.
    /// </summary>
    public ExceptionInfo? InnerException { get; init; }

    /// <summary>
    /// Initializes an empty exception info instance for deserialization.
    /// </summary>
    public ExceptionInfo()
    {
    }

    /// <summary>
    /// Initializes a new exception info instance from an exception.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="includeInnerException">Whether to capture inner exceptions.</param>
    /// <param name="includeStackTrace">Whether to capture stack traces.</param>
    /// <param name="handleAggregateExceptionAsLinkedLink">
    /// Whether aggregate exceptions should be flattened into a linked exception chain.
    /// </param>
    [SetsRequiredMembers]
    public ExceptionInfo(
        Exception exception,
        bool includeInnerException = true,
        bool includeStackTrace = true,
        bool handleAggregateExceptionAsLinkedLink = true)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (handleAggregateExceptionAsLinkedLink &&
            exception is AggregateException { InnerExceptions.Count: > 0 } aggregateException)
        {
            var exceptions = aggregateException.Flatten().InnerExceptions;
            var firstException = exceptions[0];
            Type = firstException.GetType().FullName ?? string.Empty;
            Message = firstException.Message;
            Source = firstException.Source;
            if (includeStackTrace) StackTrace = firstException.StackTrace;
            InnerException = includeInnerException
                ? CreateLinkedList(exceptions, 1, includeStackTrace)
                : null;
            return;
        }

        Type = exception.GetType().FullName ?? string.Empty;
        Message = exception.Message;
        Source = exception.Source;
        if (includeStackTrace) StackTrace = exception.StackTrace;
        if (includeInnerException && exception.InnerException is not null)
        {
            InnerException = new ExceptionInfo(
                exception.InnerException,
                includeInnerException,
                includeStackTrace,
                handleAggregateExceptionAsLinkedLink);
        }
    }

    /// <summary>
    /// Enumerates this exception and each linked inner exception.
    /// </summary>
    /// <returns>A sequence containing the linked exception chain.</returns>
    public IEnumerable<ExceptionInfo> TraverseExceptions()
    {
        var exception = this;
        do
        {
            yield return exception;
            exception = exception.InnerException;
        } while (exception is not null);
    }

    /// <summary>
    /// Recursively creates a linked list of ExceptionInfo instances from a list of exceptions, starting at the specified index.
    /// </summary>
    /// <param name="exceptions">The list of exceptions to convert.</param>
    /// <param name="index">The starting index in the list.</param>
    /// <param name="includeStackTrace">Whether to include stack traces in the ExceptionInfo instances.</param>
    /// <returns>A linked list of ExceptionInfo instances.</returns>
    private static ExceptionInfo? CreateLinkedList(
        IReadOnlyList<Exception> exceptions,
        int index,
        bool includeStackTrace)
    {
        return index >= exceptions.Count
            ? null
            : new ExceptionInfo(exceptions[index], false, includeStackTrace, false)
            {
                InnerException = CreateLinkedList(exceptions, index + 1, includeStackTrace)
            };
    }
}
