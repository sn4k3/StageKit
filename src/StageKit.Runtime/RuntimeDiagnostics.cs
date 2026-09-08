using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace StageKit.Runtime;

/// <summary>
/// Provides diagnostic information about the current runtime, process, and entry application.
/// </summary>
public static class RuntimeDiagnostics
{
    /// <summary>
    /// Returns a dictionary containing runtime, process, and entry-application diagnostic values.
    /// </summary>
    /// <returns>A dictionary containing diagnostic key-value pairs for the current process.</returns>
    public static Dictionary<string, string?> GetInfoDict()
    {
        return GetInfoDict(new RuntimeDiagnosticsOptions());
    }

    /// <summary>
    /// Returns a dictionary containing the requested runtime, process, and entry-application diagnostic values.
    /// </summary>
    /// <param name="options">Options controlling the information included in the dictionary.</param>
    /// <returns>A dictionary containing diagnostic key-value pairs for the current process.</returns>
    public static Dictionary<string, string?> GetInfoDict(RuntimeDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var info = new Dictionary<string, string?>(96)
        {
            ["Runtime.FrameworkDescription"] = RuntimeInformation.FrameworkDescription,
            ["Runtime.RuntimeIdentifier"] = RuntimeInformation.RuntimeIdentifier,
            ["Runtime.GenericRuntimeIdentifier"] = EntryApplication.GenericRuntimeIdentifier,
            ["Runtime.OSDescription"] = RuntimeInformation.OSDescription,
            ["Runtime.OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["Runtime.ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["Runtime.IsDynamicCodeSupported"] = RuntimeFeature.IsDynamicCodeSupported.ToString(),
            ["Runtime.IsDynamicCodeCompiled"] = RuntimeFeature.IsDynamicCodeCompiled.ToString(),
            ["Runtime.IsServerGC"] = GCSettings.IsServerGC.ToString(),
            ["Runtime.GCLatencyMode"] = GCSettings.LatencyMode.ToString(),
            ["System.ProcessorCount"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            ["System.Is64BitOperatingSystem"] = Environment.Is64BitOperatingSystem.ToString(),
            ["System.Uptime"] = TimeSpan.FromMilliseconds(Environment.TickCount64)
                .ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture),
            ["Process.Id"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            ["Process.Name"] = EntryApplication.ProcessName,
            ["Process.Path"] = Environment.ProcessPath,
            ["Process.BaseDirectory"] = AppContext.BaseDirectory,
            ["Process.CurrentDirectory"] = Environment.CurrentDirectory,
            ["Process.Is64BitProcess"] = Environment.Is64BitProcess.ToString(),
            ["Environment.CurrentCulture"] = CultureInfo.CurrentCulture.Name,
            ["Environment.CurrentUICulture"] = CultureInfo.CurrentUICulture.Name,
            ["Environment.TimeZone"] = TimeZoneInfo.Local.Id
        };

        if (options.IncludeProcessSnapshot)
            AddProcessSnapshot(info, GetProcessSnapshot());

        foreach (var kvp in EntryApplication.GetApplicationInfoDict())
        {
            if (kvp.Key.Contains('.'))
                info[$"{kvp.Key}"] = kvp.Value;
            else
                info[$"EntryApplication.{kvp.Key}"] = kvp.Value;
        }

        return info;
    }

    /// <summary>
    /// Captures current process, thread-pool, and managed-memory measurements.
    /// </summary>
    /// <returns>A point-in-time snapshot of the current process and managed runtime.</returns>
    public static ProcessRuntimeSnapshot GetProcessSnapshot()
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;
        var isProcessInformationAvailable = false;
        var processUptime = EntryApplication.ProcessUptime;
        var privilegedProcessorTime = TimeSpan.Zero;
        var userProcessorTime = TimeSpan.Zero;
        var workingSetBytes = Environment.WorkingSet;
        var privateMemoryBytes = 0L;
        var threadCount = 0;

        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            privilegedProcessorTime = process.PrivilegedProcessorTime;
            userProcessorTime = process.UserProcessorTime;
            workingSetBytes = process.WorkingSet64;
            privateMemoryBytes = process.PrivateMemorySize64;
            threadCount = process.Threads.Count;
            isProcessInformationAvailable = true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        ThreadPool.GetAvailableThreads(
            out var availableThreadPoolWorkerThreads,
            out var availableThreadPoolCompletionPortThreads);

        return new ProcessRuntimeSnapshot(
            capturedAtUtc,
            isProcessInformationAvailable,
            processUptime,
            privilegedProcessorTime,
            userProcessorTime,
            workingSetBytes,
            privateMemoryBytes,
            threadCount,
            ThreadPool.ThreadCount,
            availableThreadPoolWorkerThreads,
            availableThreadPoolCompletionPortThreads,
            GC.GetTotalMemory(forceFullCollection: false),
            GC.GetTotalAllocatedBytes(),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2));
    }

    /// <summary>
    /// Formats diagnostic key-value pairs as a human-readable report.
    /// </summary>
    /// <param name="info">The ordered diagnostic values to format.</param>
    /// <returns>A formatted diagnostics report.</returns>
    public static string FormatReport(IEnumerable<KeyValuePair<string, string?>> info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var sb = new StringBuilder();
        foreach (var kvp in info)
            sb.AppendLine($"{kvp.Key}: {kvp.Value?.ReplaceLineEndings("\\n")}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns a formatted diagnostic report for the current runtime, process, and entry application.
    /// </summary>
    /// <param name="includeLoadedAssemblies">True to append the currently loaded assembly list; otherwise, false.</param>
    /// <returns>A formatted diagnostic report.</returns>
    public static string GetReport(bool includeLoadedAssemblies = false)
    {
        return GetReport(new RuntimeDiagnosticsOptions
        {
            IncludeLoadedAssemblies = includeLoadedAssemblies,
        });
    }

    /// <summary>
    /// Returns a formatted diagnostic report using the requested options.
    /// </summary>
    /// <param name="options">Options controlling the information included in the report.</param>
    /// <returns>A formatted diagnostic report.</returns>
    public static string GetReport(RuntimeDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var report = FormatReport(GetInfoDict(options));
        if (!options.IncludeLoadedAssemblies)
            return report;

        var sb = new StringBuilder(report.Length + EntryApplication.FormattedLoadedAssemblies.Length + 32);
        if (report.Length > 0)
        {
            sb.AppendLine(report);
            sb.AppendLine();
        }

        sb.AppendLine("Loaded Assemblies:");
        sb.Append(EntryApplication.FormattedLoadedAssemblies);

        return sb.ToString().TrimEnd();
    }

    private static void AddProcessSnapshot(
        IDictionary<string, string?> info,
        ProcessRuntimeSnapshot snapshot)
    {
        info["Process.CapturedAtUtc"] = snapshot.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture);
        info["Process.IsInformationAvailable"] = snapshot.IsProcessInformationAvailable.ToString();
        info["Process.Uptime"] = snapshot.ProcessUptime.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);
        info["Process.PrivilegedProcessorTime"] =
            snapshot.PrivilegedProcessorTime.ToString("c", CultureInfo.InvariantCulture);
        info["Process.UserProcessorTime"] = snapshot.UserProcessorTime.ToString("c", CultureInfo.InvariantCulture);
        info["Process.TotalProcessorTime"] = snapshot.TotalProcessorTime.ToString("c", CultureInfo.InvariantCulture);
        info["Process.WorkingSetBytes"] = snapshot.WorkingSetBytes.ToString(CultureInfo.InvariantCulture);
        info["Process.PrivateMemoryBytes"] = snapshot.PrivateMemoryBytes.ToString(CultureInfo.InvariantCulture);
        info["Process.ThreadCount"] = snapshot.ThreadCount.ToString(CultureInfo.InvariantCulture);
        info["ThreadPool.ThreadCount"] = snapshot.ThreadPoolThreadCount.ToString(CultureInfo.InvariantCulture);
        info["ThreadPool.AvailableWorkerThreads"] =
            snapshot.AvailableThreadPoolWorkerThreads.ToString(CultureInfo.InvariantCulture);
        info["ThreadPool.AvailableCompletionPortThreads"] =
            snapshot.AvailableThreadPoolCompletionPortThreads.ToString(CultureInfo.InvariantCulture);
        info["GC.ManagedHeapBytes"] = snapshot.ManagedHeapBytes.ToString(CultureInfo.InvariantCulture);
        info["GC.TotalAllocatedBytes"] = snapshot.TotalAllocatedBytes.ToString(CultureInfo.InvariantCulture);
        info["GC.Generation0CollectionCount"] =
            snapshot.Generation0CollectionCount.ToString(CultureInfo.InvariantCulture);
        info["GC.Generation1CollectionCount"] =
            snapshot.Generation1CollectionCount.ToString(CultureInfo.InvariantCulture);
        info["GC.Generation2CollectionCount"] =
            snapshot.Generation2CollectionCount.ToString(CultureInfo.InvariantCulture);
    }
}
