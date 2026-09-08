namespace StageKit.Primitives.System;

/// <summary>
/// Represents a snapshot of the host's physical memory.
/// </summary>
public readonly record struct HostMemoryStatus
{
    internal HostMemoryStatus(ulong totalPhysicalBytes, ulong availablePhysicalBytes)
    {
        TotalPhysicalBytes = totalPhysicalBytes;
        AvailablePhysicalBytes = Math.Min(availablePhysicalBytes, totalPhysicalBytes);
    }

    /// <summary>
    /// Gets the physical memory visible to the operating system, in bytes.
    /// </summary>
    public ulong TotalPhysicalBytes { get; }

    /// <summary>
    /// Gets the physical memory currently available for reuse, in bytes.
    /// </summary>
    public ulong AvailablePhysicalBytes { get; }

    /// <summary>
    /// Gets the physical memory currently in use, in bytes.
    /// </summary>
    public ulong UsedPhysicalBytes => TotalPhysicalBytes - AvailablePhysicalBytes;

    /// <summary>
    /// Gets the percentage of physical memory currently in use.
    /// </summary>
    public double MemoryLoadPercentage => TotalPhysicalBytes == 0
        ? 0
        : UsedPhysicalBytes * 100d / TotalPhysicalBytes;
}