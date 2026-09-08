namespace StageKit.Runtime;

/// <summary>
/// Represents a point-in-time snapshot of the current process and managed runtime.
/// </summary>
/// <param name="CapturedAtUtc">The UTC time at which the snapshot was captured.</param>
/// <param name="IsProcessInformationAvailable">
/// Whether operating-system process information was available when the snapshot was captured.
/// </param>
/// <param name="ProcessUptime">The elapsed time since the current process started.</param>
/// <param name="PrivilegedProcessorTime">The processor time spent executing operating-system code.</param>
/// <param name="UserProcessorTime">The processor time spent executing application code.</param>
/// <param name="WorkingSetBytes">The current physical memory assigned to the process, in bytes.</param>
/// <param name="PrivateMemoryBytes">The current private memory allocated for the process, in bytes.</param>
/// <param name="ThreadCount">The number of operating-system threads in the process.</param>
/// <param name="ThreadPoolThreadCount">The number of managed thread-pool threads.</param>
/// <param name="AvailableThreadPoolWorkerThreads">The number of available thread-pool worker threads.</param>
/// <param name="AvailableThreadPoolCompletionPortThreads">
/// The number of available thread-pool asynchronous I/O threads.
/// </param>
/// <param name="ManagedHeapBytes">The current managed heap size, in bytes.</param>
/// <param name="TotalAllocatedBytes">The total managed bytes allocated during the process lifetime.</param>
/// <param name="Generation0CollectionCount">The number of generation 0 garbage collections.</param>
/// <param name="Generation1CollectionCount">The number of generation 1 garbage collections.</param>
/// <param name="Generation2CollectionCount">The number of generation 2 garbage collections.</param>
public sealed record ProcessRuntimeSnapshot(
    DateTimeOffset CapturedAtUtc,
    bool IsProcessInformationAvailable,
    TimeSpan ProcessUptime,
    TimeSpan PrivilegedProcessorTime,
    TimeSpan UserProcessorTime,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    int ThreadCount,
    int ThreadPoolThreadCount,
    int AvailableThreadPoolWorkerThreads,
    int AvailableThreadPoolCompletionPortThreads,
    long ManagedHeapBytes,
    long TotalAllocatedBytes,
    int Generation0CollectionCount,
    int Generation1CollectionCount,
    int Generation2CollectionCount)
{
    /// <summary>
    /// Gets the total processor time used by the process.
    /// </summary>
    public TimeSpan TotalProcessorTime => PrivilegedProcessorTime + UserProcessorTime;
}
