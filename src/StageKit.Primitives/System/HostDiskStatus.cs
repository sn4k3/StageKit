using System.IO;

namespace StageKit.Primitives.System;

/// <summary>
/// Represents a snapshot of disk capacity and free space for a drive or volume.
/// </summary>
public readonly record struct HostDiskStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostDiskStatus"/> struct.
    /// </summary>
    /// <param name="driveName">The name or mount point of the drive.</param>
    /// <param name="totalSizeBytes">The total size of the drive in bytes.</param>
    /// <param name="availableFreeSpaceBytes">The free space available to the calling user in bytes.</param>
    /// <param name="totalFreeSpaceBytes">The total amount of free space on the drive in bytes.</param>
    /// <param name="driveFormat">The file system format (e.g. NTFS, APFS, ext4), or <see langword="null"/> if unavailable.</param>
    /// <param name="driveType">The drive type (e.g. Fixed, Removable, Network).</param>
    public HostDiskStatus(
        string driveName,
        long totalSizeBytes,
        long availableFreeSpaceBytes,
        long totalFreeSpaceBytes,
        string? driveFormat = null,
        DriveType driveType = DriveType.Unknown)
    {
        DriveName = driveName;
        TotalSizeBytes = totalSizeBytes;
        AvailableFreeSpaceBytes = availableFreeSpaceBytes;
        TotalFreeSpaceBytes = totalFreeSpaceBytes;
        DriveFormat = driveFormat;
        DriveType = driveType;
    }

    /// <summary>
    /// Gets the name or mount point of the drive.
    /// </summary>
    public string DriveName { get; }

    /// <summary>
    /// Gets the total capacity of the drive in bytes.
    /// </summary>
    public long TotalSizeBytes { get; }

    /// <summary>
    /// Gets the amount of free disk space available to the calling user in bytes.
    /// </summary>
    public long AvailableFreeSpaceBytes { get; }

    /// <summary>
    /// Gets the total amount of free disk space on the drive in bytes.
    /// </summary>
    public long TotalFreeSpaceBytes { get; }

    /// <summary>
    /// Gets the file system format name (e.g., NTFS, APFS, ext4), or <see langword="null"/> if unavailable.
    /// </summary>
    public string? DriveFormat { get; }

    /// <summary>
    /// Gets the drive type (e.g. Fixed, Removable, Network, Unknown).
    /// </summary>
    public DriveType DriveType { get; }

    /// <summary>
    /// Gets the total capacity of the drive in gigabytes (GB).
    /// </summary>
    public double TotalSizeGigabytes => TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);

    /// <summary>
    /// Gets the available free disk space in gigabytes (GB).
    /// </summary>
    public double AvailableFreeSpaceGigabytes => AvailableFreeSpaceBytes / (1024.0 * 1024.0 * 1024.0);
}
