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
    /// <returns><see langword="true"/> when the path is equal to or contained under the root path; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> or <paramref name="rootPath"/> is null or whitespace.</exception>
    public static bool IsSubPathOf(string path, string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullPath = TrimDirectorySeparators(Path.GetFullPath(path));
        var fullRoot = TrimDirectorySeparators(Path.GetFullPath(rootPath));

        return string.Equals(fullPath, fullRoot, PlatformPathComparison)
               || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, PlatformPathComparison)
               || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, PlatformPathComparison);
    }

    /// <summary>
    /// Normalizes a path for use as a zip archive entry name.
    /// </summary>
    /// <param name="entryName">The entry name to normalize.</param>
    /// <returns>The normalized archive entry name.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="entryName"/> is null or whitespace.</exception>
    public static string NormalizeArchiveEntryName(string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);

        return entryName
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string TrimDirectorySeparators(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
