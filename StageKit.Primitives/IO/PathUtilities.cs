namespace StageKit.Primitives;

/// <summary>
/// Provides cross-platform path helper methods.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Gets the string comparison used for file-system paths on the current platform.
    /// </summary>
    public static StringComparison PlatformPathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Determines whether a path is equal to or contained under the specified root path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="rootPath">The root path to compare against.</param>
    /// <returns><see langword="true"/> when the path is equal to or contained under the root path without traversing a
    /// reparse point; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> or <paramref name="rootPath"/> is null or whitespace.</exception>
    public static bool IsSubPathOf(string path, string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(rootPath);
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar) ||
                                fullRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        var isWithinRoot = string.Equals(fullPath, fullRoot, PlatformPathComparison) ||
                           fullPath.StartsWith(rootWithSeparator, PlatformPathComparison);

        return isWithinRoot && !ContainsReparsePoint(fullPath, fullRoot);
    }

    /// <summary>
    /// Normalizes a path for use as a zip archive entry name by converting both Windows and platform directory
    /// separators to forward slashes.
    /// </summary>
    /// <param name="entryName">The entry name to normalize.</param>
    /// <returns>The normalized archive entry name.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="entryName"/> is null or whitespace.</exception>
    public static string NormalizeArchiveEntryName(string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);

        return entryName
            .Replace('\\', '/')
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool ContainsReparsePoint(string fullPath, string fullRoot)
    {
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (relativePath == ".") return false;

        var currentPath = fullRoot;
        try
        {
            var rootInfo = new DirectoryInfo(currentPath);
            if (rootInfo.Exists && rootInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
        {
            // Failing closed avoids treating an uninspectable root as safe.
            return true;
        }

        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);

            try
            {
                var directoryInfo = new DirectoryInfo(currentPath);
                if (directoryInfo.Exists)
                {
                    if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
                    continue;
                }

                var fileInfo = new FileInfo(currentPath);
                if (fileInfo.Exists && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
            {
                // Failing closed avoids treating an uninspectable path as safe.
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tries to get a valid full directory path from the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path to validate.</param>
    /// <param name="fullDirectoryPath">The full directory path if valid.</param>
    /// <returns>True if the directory path is valid; otherwise, false.</returns>
    public static bool TryGetValidFullDirectoryPath(string directoryPath, out string fullDirectoryPath)
    {
        fullDirectoryPath = string.Empty;

        try
        {
            fullDirectoryPath = Path.GetFullPath(directoryPath);
            var rootPath = Path.GetPathRoot(fullDirectoryPath);
            var pathWithoutRoot = rootPath is null
                ? fullDirectoryPath
                : fullDirectoryPath[rootPath.Length..];
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var segments = pathWithoutRoot.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            return segments.All(segment => segment.IndexOfAny(invalidFileNameChars) < 0);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                       or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified directory path is writable or can be created and returns the full directory path if valid.
    /// </summary>
    /// <param name="directoryPath">The directory path to check.</param>
    /// <param name="fullDirectoryPath">The full directory path if valid.</param>
    /// <returns>True if the directory path is writable or can be created; otherwise, false.</returns>
    public static bool IsWritableOrCreatableDirectory(string directoryPath, out string fullDirectoryPath)
    {
        fullDirectoryPath = string.Empty;
        if (!TryGetValidFullDirectoryPath(directoryPath, out fullDirectoryPath)) return false;
        if (File.Exists(fullDirectoryPath)) return false;
        if (Directory.Exists(fullDirectoryPath)) return IsWritableDirectory(fullDirectoryPath);

        var parent = Directory.GetParent(fullDirectoryPath);
        while (parent is not null && !Directory.Exists(parent.FullName)) parent = parent.Parent;

        return parent is not null && IsWritableDirectory(parent.FullName);
    }

    /// <summary>
    /// Determines whether the specified directory path is writable by attempting to create and delete a temporary file in it.
    /// </summary>
    /// <param name="directoryPath">The directory path to check.</param>
    /// <returns>True if the directory is writable; otherwise, false.</returns>
    public static bool IsWritableDirectory(string directoryPath)
    {
        try
        {
            var probePath = Path.Combine(directoryPath, $".stagekit-write-test-{Guid.NewGuid():N}.tmp");
            using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);

            probe.WriteByte(0);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                       or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified argument path is the same as the specified path, considering platform-specific case sensitivity.
    /// </summary>
    /// <param name="path1">The first path to compare.</param>
    /// <param name="path2">The second path to compare.</param>
    /// <returns>True if the paths are the same; otherwise, false.</returns>
    public static bool IsSamePath(string? path1, string? path2)
    {
        if (path1 is null && path2 is null) return true;
        if (path1 is null || path2 is null) return false;

        try
        {
            return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2),
                SystemUtilities.SystemStringComparison);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                       or UnauthorizedAccessException)
        {
            // Ignored
        }

        return false;
    }
}