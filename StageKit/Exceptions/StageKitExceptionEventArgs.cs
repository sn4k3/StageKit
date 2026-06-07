using System.Collections.ObjectModel;

namespace StageKit;

/// <summary>
/// Provides data for unhandled exceptions in StageKit, including optional category and custom data for crash reporting.
/// </summary>
public class StageKitExceptionEventArgs : UnhandledExceptionEventArgs
{
    /// <summary>
    /// Gets the application-defined category for the exception, which can be used for crash reporting and analytics.
    /// </summary>
    public string Category { get; init; } = "[Uncategorized]";

    /// <summary>
    /// Gets a value indicating whether the exception is ignored for crash reporting purposes. If true, the exception will not be included in crash reports.
    /// </summary>
    /// <remarks>If <see langword="true"/> the application will try to continue and not kill itself.</remarks>
    public bool IsIgnored { get; init; }

    /// <summary>
    /// Gets an optional read-only dictionary of custom data that can be included in crash reports for additional context and diagnostics.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? CustomData { get; set; }

    /// <summary>
    /// Converts the current exception event arguments into a <see cref="CrashReport"/> instance, which can be used for logging and crash reporting purposes.
    /// </summary>
    /// <returns>A <see cref="CrashReport"/> instance containing the exception details and custom data.</returns>
    public CrashReport ToCrashReport()
    {
        return new CrashReport(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StageKitExceptionEventArgs"/> class based on an existing <see cref="UnhandledExceptionEventArgs"/> instance, copying relevant properties and custom data if available.
    /// </summary>
    /// <param name="args">The existing <see cref="UnhandledExceptionEventArgs"/> instance to copy properties from.</param>
    public StageKitExceptionEventArgs(UnhandledExceptionEventArgs args) : base(args.ExceptionObject, args.IsTerminating)
    {
        if (args is StageKitExceptionEventArgs stageKitArgs)
        {
            Category = stageKitArgs.Category;
            IsIgnored = stageKitArgs.IsIgnored;
            CustomData = stageKitArgs.CustomData;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StageKitExceptionEventArgs"/> class based on an existing <see cref="UnobservedTaskExceptionEventArgs"/> instance, copying relevant properties and marking the exception as observed to prevent application termination.
    /// </summary>
    /// <param name="args">The existing <see cref="UnobservedTaskExceptionEventArgs"/> instance to copy properties from.</param>
    public StageKitExceptionEventArgs(UnobservedTaskExceptionEventArgs args) : base(args.Exception, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StageKitExceptionEventArgs"/> class with the specified exception and termination status.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="isTerminating">Indicates whether the application is terminating.</param>
    public StageKitExceptionEventArgs(object exception, bool isTerminating) : base(exception, isTerminating)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StageKitExceptionEventArgs"/> class with the specified exception, category, and termination status.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="category">The application-defined category for the exception.</param>
    /// <param name="isTerminating">Indicates whether the application is terminating.</param>
    public StageKitExceptionEventArgs(object exception, string? category, bool isTerminating) : base(exception, isTerminating)
    {
        if (category is not null) Category = category;
    }
}