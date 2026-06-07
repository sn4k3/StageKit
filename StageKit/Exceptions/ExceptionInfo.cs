// asdasdasd

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    /// <summary>
    /// Gets the exception stack trace.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets the next exception in the linked exception chain.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

        var exceptionType = exception.GetType();
        Type = exceptionType.FullName ?? exceptionType.Name;
        Message = exception.Message;
        Source = exception.Source;
        if (includeStackTrace) StackTrace = exception.StackTrace;

        if (!includeInnerException)
        {
            return;
        }

        var innerExceptions = handleAggregateExceptionAsLinkedLink
            ? UnhandledExceptions.TraverseExceptions(exception).Skip(1)
            : TraverseInnerExceptions(exception);

        InnerException = CreateLinkedList(innerExceptions, includeStackTrace);
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
    /// Creates a linked list of <see cref="ExceptionInfo"/> instances from a sequence of exceptions.
    /// </summary>
    /// <param name="exceptions">The list of exceptions to convert.</param>
    /// <param name="includeStackTrace">Whether to include stack traces in the ExceptionInfo instances.</param>
    /// <returns>A linked list of ExceptionInfo instances.</returns>
    private static ExceptionInfo? CreateLinkedList(
        IEnumerable<Exception> exceptions,
        bool includeStackTrace)
    {
        ExceptionInfo? linkedException = null;

        foreach (var exception in exceptions.Reverse())
        {
            linkedException = new ExceptionInfo(exception, false, includeStackTrace, false)
            {
                InnerException = linkedException
            };
        }

        return linkedException;
    }

    /// <summary>
    /// Enumerates the direct inner exception chain without expanding aggregate exceptions.
    /// </summary>
    /// <param name="exception">The exception whose inner exceptions should be enumerated.</param>
    /// <returns>The direct inner exception chain.</returns>
    private static IEnumerable<Exception> TraverseInnerExceptions(Exception exception)
    {
        var innerException = exception.InnerException;
        while (innerException is not null)
        {
            yield return innerException;
            innerException = innerException.InnerException;
        }
    }
}