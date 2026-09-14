using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
    /// <summary>
    /// Gets the available free disk space in bytes for the drive containing the specified path.
    /// </summary>
    /// <param name="path">The file, directory, or drive path to query.</param>
    /// <returns>The number of available free bytes for the calling user.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when disk space information cannot be determined.</exception>
    public static long GetAvailableFreeSpace(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (TryGetAvailableFreeSpace(path, out var freeBytes))
            return freeBytes;

        throw new IOException($"Could not determine available free disk space for path '{path}'.");
    }

    /// <summary>
    /// Tries to get the available free disk space in bytes for the drive containing the specified path.
    /// </summary>
    /// <param name="path">The file, directory, or drive path to query.</param>
    /// <param name="freeBytes">When this method returns, contains the available free bytes if queried successfully; otherwise, 0.</param>
    /// <returns><see langword="true"/> if free disk space was successfully queried; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetAvailableFreeSpace(string path, out long freeBytes)
    {
        freeBytes = 0;

        if (TryGetDiskStatus(path, out var status))
        {
            freeBytes = status.Value.AvailableFreeSpaceBytes;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets disk capacity and status information for the drive containing the specified path.
    /// </summary>
    /// <param name="path">The file, directory, or drive path to query.</param>
    /// <returns>A <see cref="HostDiskStatus"/> snapshot for the drive containing the path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when disk information cannot be determined.</exception>
    public static HostDiskStatus GetDiskStatus(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (TryGetDiskStatus(path, out var status) && status.HasValue)
            return status.Value;

        throw new IOException($"Could not determine disk status for path '{path}'.");
    }

    /// <summary>
    /// Tries to get disk capacity and status information for the drive containing the specified path.
    /// </summary>
    /// <param name="path">The file, directory, or drive path to query.</param>
    /// <param name="status">When this method returns, contains the disk status snapshot if queried successfully; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if disk status was successfully queried; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetDiskStatus(string path, [NotNullWhen(true)] out HostDiskStatus? status)
    {
        status = null;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
            return TryGetWindowsDiskStatus(fullPath, out status);

        return TryGetUnixDiskStatus(fullPath, out status);
    }

    private static bool TryGetWindowsDiskStatus(string fullPath, [NotNullWhen(true)] out HostDiskStatus? status)
    {
        status = null;

        try
        {
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
                return false;

            if (GetDiskFreeSpaceEx(root, out var freeBytesAvailable, out var totalBytes, out var totalFreeBytes))
            {
                string? format = null;
                var driveType = DriveType.Unknown;

                try
                {
                    var driveInfo = new DriveInfo(root);
                    if (driveInfo.IsReady)
                    {
                        format = driveInfo.DriveFormat;
                        driveType = driveInfo.DriveType;
                    }
                }
                catch
                {
                    // DriveInfo failure should not prevent returning free space
                }

                status = new HostDiskStatus(
                    root,
                    (long)totalBytes,
                    (long)freeBytesAvailable,
                    (long)totalFreeBytes,
                    format,
                    driveType);
                return true;
            }
        }
        catch
        {
            // Fallback to DriveInfo
        }

        return TryGetDriveInfoStatus(fullPath, out status);
    }

    private static bool TryGetUnixDiskStatus(string fullPath, [NotNullWhen(true)] out HostDiskStatus? status)
    {
        return TryGetDriveInfoStatus(fullPath, out status);
    }

    private static bool TryGetDriveInfoStatus(string fullPath, [NotNullWhen(true)] out HostDiskStatus? status)
    {
        status = null;

        try
        {
            var drive = FindMatchingDrive(fullPath);
            if (drive is not null && drive.IsReady)
            {
                status = new HostDiskStatus(
                    drive.Name,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    drive.TotalFreeSpace,
                    drive.DriveFormat,
                    drive.DriveType);
                return true;
            }
        }
        catch
        {
            // Best effort
        }

        return false;
    }

    private static DriveInfo? FindMatchingDrive(string fullPath)
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            DriveInfo? bestMatch = null;
            var bestLength = -1;

            foreach (var drive in drives)
            {
                if (!drive.IsReady) continue;
                var root = drive.RootDirectory.FullName;
                if (fullPath.StartsWith(root, HostStringComparison) && root.Length > bestLength)
                {
                    bestMatch = drive;
                    bestLength = root.Length;
                }
            }

            return bestMatch;
        }
        catch
        {
            return null;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailableToCaller,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
}
