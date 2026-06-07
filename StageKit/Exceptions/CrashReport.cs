using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using StageKit.Runtime;

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
    public string Version { get; init; } = EntryApplication.AssemblyVersionString ?? string.Empty;

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
    public DateTime DateTimeUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the local timestamp when the crash report was created.
    /// </summary>
    public DateTime DateTime => DateTimeUtc.ToLocalTime();

    /// <summary>
    /// Gets the managed thread pool thread count captured at crash time.
    /// </summary>
    public int ThreadPoolCount { get; init; } = ThreadPool.ThreadCount;

    /// <summary>
    /// Gets the process thread count captured at crash time.
    /// </summary>
    public int ThreadCount { get; init; }

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
    public long ProcessWorkingSet64 { get; init; } = Environment.WorkingSet;

    /// <summary>
    /// Gets the total managed heap bytes captured at crash time.
    /// </summary>
    public long GcTotalMemory { get; init; } = GC.GetTotalMemory(false);

    /// <summary>
    /// Gets the total bytes allocated by the process over its lifetime, captured at crash time.
    /// </summary>
    public long GcTotalAllocatedBytes { get; init; } = GC.GetTotalAllocatedBytes();

    /// <summary>
    /// Gets the number of garbage collections per generation (index 0, 1, 2) captured at crash time.
    /// </summary>
    public int[] GcCollectionCounts { get; init; } = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];

    /// <summary>
    /// Gets the elapsed runtime of the system at crash time. This value is calculated from the moment the system began until the crash report was created.
    /// </summary>
    public TimeSpan SystemElapsedRuntime { get; init; } = TimeSpan.FromMilliseconds(Environment.TickCount64);

    /// <summary>
    /// Gets the elapsed runtime of the application at crash time. This value is calculated from the moment the application process began until the crash report was created.
    /// </summary>
    public TimeSpan ProgramElapsedRuntime { get; init; } = ApplicationKit.RuntimeElapsed;

    /// <summary>
    /// Gets a dictionary of custom key-value data to include in the crash report. This can be used to add application-specific information relevant to the crash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? CustomData { get; init; }

    /// <summary>
    /// Gets a formatted, human-readable crash report message.
    /// </summary>
    [JsonIgnore]
    public string FormattedMessage
    {
        get
        {
            var sb = new StringBuilder(2048);
            var exceptions = Exception.EnumerateExceptions().ToArray();

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
            sb.AppendLine($"Processor cores: {Environment.ProcessorCount}   Runtime: {RuntimeInformation.RuntimeIdentifier}");
            sb.AppendLine($"Thread usage: {ThreadPoolCount} / {ThreadCount}");
            sb.AppendLine($"CPU usage: System={CpuPrivilegedTime}   User={CpuUserTime}   Total={CpuTotalTime}");
            sb.AppendLine($"RAM usage: {Helpers.ToFileSizeString(ProcessWorkingSet64)}");
            sb.AppendLine($"GC heap: {Helpers.ToFileSizeString(GcTotalMemory)}   Allocated: {Helpers.ToFileSizeString(GcTotalAllocatedBytes)}   Collections: {string.Join('/', GcCollectionCounts.Select(i => Helpers.ToFileSizeString(i)))}");
            sb.AppendLine($"System elapsed: {SystemElapsedRuntime}");
            sb.AppendLine($"Program elapsed: {ProgramElapsedRuntime}");

            if (CustomData is not null)
            {
                foreach (var data in CustomData)
                {
                    sb.AppendLine($"{data.Key}: {data.Value}");
                }
            }

            FormatMessageFunc?.Invoke(this, sb);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Initializes a new empty crash report for deserialization.
    /// </summary>
    public CrashReport()
    {
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
            // ignored
        }
    }

    /// <summary>
    /// Initializes a new crash report for the specified exception and timestamp.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="category">The application-defined crash category.</param>
    /// <param name="dateTimeUtc">The UTC timestamp to store in the report.</param>
    /// <param name="customData">Optional custom data to include in the crash report.</param>
    [SetsRequiredMembers]
    public CrashReport(Exception exception, string category, DateTime dateTimeUtc, IReadOnlyDictionary<string, object?>? customData = null) : this()
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception = new ExceptionInfo(exception);
        Category = category;
        DateTimeUtc = dateTimeUtc;
        CustomData = customData;
    }

    /// <summary>
    /// Initializes a new crash report for the specified exception using the current UTC time.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="category">The application-defined crash category.</param>
    /// <param name="customData">Optional custom data to include in the crash report.</param>
    [SetsRequiredMembers]
    public CrashReport(Exception exception, string category, IReadOnlyDictionary<string, object?>? customData = null) : this(exception, category, DateTime.UtcNow, customData)
    {
    }

    /// <summary>
    /// Initializes a new crash report based on the provided <see cref="StageKitExceptionEventArgs"/>, extracting the exception, category, timestamp, and custom data for the report.
    /// </summary>
    /// <param name="args">The exception event arguments to extract data from.</param>
    [SetsRequiredMembers]
    public CrashReport(StageKitExceptionEventArgs args) : this((Exception)args.ExceptionObject, args.Category, DateTime.UtcNow, args.CustomData)
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
