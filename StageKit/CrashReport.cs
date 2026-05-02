using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace StageKit;

/// <summary>
/// Captures exception and runtime state for later crash report display or persistence.
/// </summary>
public record CrashReport
{
    /// <summary>
    /// Gets or sets an optional callback that can append custom text to <see cref="FormattedMessage"/>.
    /// </summary>
    public static Action<CrashReport, StringBuilder>? FormatMessageFunc { get; set; }

    /// <summary>
    /// Gets the unique crash report identifier.
    /// </summary>
    public long Id { get; init; } = Stopwatch.GetTimestamp();

    /// <summary>
    /// Gets the application version captured when the report was created.
    /// </summary>
    public string Version { get; init; } = Helpers.AssemblyVersionString ?? string.Empty;

    /// <summary>
    /// Gets the captured exception information.
    /// </summary>
    public required ExceptionInfo Exception { get; init; }

    /// <summary>
    /// Gets the application-defined crash category.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the crash report was created.
    /// </summary>
    public required DateTime DateTimeUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the local timestamp when the crash report was created.
    /// </summary>
    public DateTime DateTime => DateTimeUtc.ToLocalTime();

    /// <summary>
    /// Gets the managed thread pool thread count captured at crash time.
    /// </summary>
    public int ThreadPoolCount { get; init; }

    /// <summary>
    /// Gets the process thread count captured at crash time.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the elapsed runtime of the application at crash time. This value is calculated from the moment the application process began until the crash report was created.
    /// </summary>
    public TimeSpan ElapsedRuntime { get; init; }

    /// <summary>
    /// Gets the privileged CPU time used by the process at crash time.
    /// </summary>
    public TimeSpan CpuPrivilegedTime { get; init; }

    /// <summary>
    /// Gets the user CPU time used by the process at crash time.
    /// </summary>
    public TimeSpan CpuUserTime { get; init; }

    /// <summary>
    /// Gets the total CPU time used by the process at crash time.
    /// </summary>
    public TimeSpan CpuTotalTime => CpuPrivilegedTime + CpuUserTime;

    /// <summary>
    /// Gets the process working set size captured at crash time.
    /// </summary>
    public long ProcessWorkingSet64 { get; init; }

    /// <summary>
    /// Gets the elapsed runtime of the system at crash time. This value is calculated from the moment the system began until the crash report was created.
    /// </summary>
    public TimeSpan SystemElapsedRuntime { get; init; }

    /// <summary>
    /// Gets a formatted, human-readable crash report message.
    /// </summary>
    [JsonIgnore]
    public string FormattedMessage
    {
        get
        {
            var sb = new StringBuilder(2048);
            var exceptions = Exception.TraverseExceptions().ToArray();

            for (var i = 0; i < exceptions.Length; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                if (exceptions.Length > 1)
                {
                    sb.AppendLine($"## {i + 1} ##");
                }

                AppendException(sb, exceptions[i]);
            }

            sb.AppendLine();
            sb.AppendLine($"ID: {Id}");
            sb.AppendLine($"Version: {Version} {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"Category: {Category}");
            sb.AppendLine($"UTC date time: {DateTimeUtc}");
            sb.AppendLine($"Machine date time: {DateTime}");
            sb.AppendLine($"Operating System: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");

            if (!string.IsNullOrWhiteSpace(ApplicationKit.UiFrameworkInfo))
            {
                sb.AppendLine($"UI: {ApplicationKit.UiFrameworkInfo}");
            }

            sb.AppendLine($"Runtime: {RuntimeInformation.RuntimeIdentifier}");
            sb.AppendLine($"Processor cores: {Environment.ProcessorCount}   Thread usage: {ThreadPoolCount} / {ThreadCount}");
            sb.AppendLine($"Elapsed: {ElapsedRuntime}");
            sb.AppendLine($"CPU usage: System={CpuPrivilegedTime}   User={CpuUserTime}   Total={CpuTotalTime}");
            sb.AppendLine($"RAM usage: {Helpers.ToFileSizeString(ProcessWorkingSet64)}");
            sb.AppendLine($"System elapsed: {SystemElapsedRuntime}");
            FormatMessageFunc?.Invoke(this, sb);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Initializes a new empty crash report for deserialization.
    /// </summary>
    public CrashReport()
    {
    }

    /// <summary>
    /// Initializes a new crash report for the specified exception and timestamp.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="category">The application-defined crash category.</param>
    /// <param name="dateTimeUtc">The UTC timestamp to store in the report.</param>
    [SetsRequiredMembers]
    public CrashReport(Exception exception, string category, DateTime dateTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception = new ExceptionInfo(exception);
        Category = category;
        DateTimeUtc = dateTimeUtc;
        ThreadPoolCount = ThreadPool.ThreadCount;
        ElapsedRuntime = ApplicationKit.RuntimeElapsed;
        SystemElapsedRuntime = TimeSpan.FromMilliseconds(Environment.TickCount64);

        try
        {
            using var process = Process.GetCurrentProcess();
            ThreadCount = process.Threads.Count;
            CpuPrivilegedTime = process.PrivilegedProcessorTime;
            CpuUserTime = process.UserProcessorTime;
            ProcessWorkingSet64 = process.WorkingSet64;
        }
        catch
        {
            ProcessWorkingSet64 = Environment.WorkingSet;
        }
    }

    /// <summary>
    /// Initializes a new crash report for the specified exception using the current UTC time.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="category">The application-defined crash category.</param>
    [SetsRequiredMembers]
    public CrashReport(Exception exception, string category) : this(exception, category, DateTime.UtcNow)
    {
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return FormattedMessage;
    }

    /// <summary>
    /// Appends the details of an exception to the provided StringBuilder, including message, type, source, and stack trace.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the exception details to.</param>
    /// <param name="exception">The exception information to append.</param>
    private static void AppendException(StringBuilder sb, ExceptionInfo exception)
    {
        sb.AppendLine(exception.Message);
        sb.AppendLine(exception.Type);

        if (!string.IsNullOrWhiteSpace(exception.Source))
        {
            sb.AppendLine(exception.Source);
        }

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            sb.AppendLine(exception.StackTrace);
        }
    }
}
