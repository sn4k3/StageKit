using System.Diagnostics.CodeAnalysis;

namespace StageKit.Primitives.System;

/// <summary>
/// Provides cross-platform system helper methods.
/// </summary>
public static class HostSystem
{
    /// <summary>
    /// Gets the string comparison used for file-system paths on the current platform.
    /// </summary>
    public static StringComparison HostStringComparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Normalize the executable extension for the current OS.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>Normalized executable with the extension.</returns>
    public static string NormalizeExecutableExtension(string path)
    {
        return OperatingSystem.IsWindows() && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(path, ".exe")
            : path;
    }

    /// <summary>
    /// Tries to resolve an executable using the current host's executable search rules.
    /// </summary>
    /// <param name="executable">The executable name or path to resolve.</param>
    /// <param name="result">
    /// The absolute path to the executable if found; otherwise, <c>null</c>.
    /// </param>
    /// <returns><see langword="true"/> if the executable was found; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Windows searches the current and system directories before <c>PATH</c> and honors <c>PATHEXT</c>. Unix
    /// searches only directories represented in <c>PATH</c>, including empty entries as the current directory, and
    /// requires at least one executable mode bit. An explicit path is checked directly without searching.
    /// </remarks>
    public static bool TryFindExecutable(string executable, [NotNullWhen(true)] out string? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(executable))
            return false;

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var executableExtensions = isWindows ? GetWindowsExecutableExtensions() : [];

            if (Path.IsPathRooted(executable) || PathUtilities.ContainsDirectorySeparator(executable))
            {
                return TryExecutablePath(
                    Path.GetFullPath(executable),
                    executableExtensions,
                    out result);
            }

            if (isWindows)
            {
                if (TryDirectory(Environment.CurrentDirectory, executable, executableExtensions, out result))
                    return true;

                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);

                if (!string.IsNullOrEmpty(systemDirectory) &&
                    TryDirectory(systemDirectory, executable, executableExtensions, out result))
                {
                    return true;
                }

                if (Environment.Is64BitOperatingSystem)
                {
                    var systemX86Directory = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

                    if (!string.IsNullOrEmpty(systemX86Directory) &&
                        TryDirectory(systemX86Directory, executable, executableExtensions, out result))
                    {
                        return true;
                    }
                }

                var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                if (!string.IsNullOrEmpty(windowsDirectory) &&
                    TryDirectory(windowsDirectory, executable, executableExtensions, out result))
                {
                    return true;
                }
            }

            var environmentPath = Environment.GetEnvironmentVariable("PATH");

            if (environmentPath is null)
                return false;

            var remaining = environmentPath.AsSpan();

            while (true)
            {
                var separatorIndex = remaining.IndexOf(Path.PathSeparator);

                var directory = separatorIndex >= 0
                    ? remaining[..separatorIndex]
                    : remaining;

                if (isWindows)
                    directory = directory.Trim();

                // Quoted PATH entries occasionally occur on Windows.
                if (isWindows &&
                    directory.Length >= 2 &&
                    directory[0] == '"' &&
                    directory[^1] == '"')
                {
                    directory = directory[1..^1];
                }

                if (directory.IsEmpty)
                    directory = Environment.CurrentDirectory;

                if (TryDirectory(directory, executable, executableExtensions, out result))
                    return true;

                if (separatorIndex < 0)
                    break;

                remaining = remaining[(separatorIndex + 1)..];
            }

            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          IOException or
                                          UnauthorizedAccessException or
                                          NotSupportedException)
        {
            result = null;
            return false;
        }
    }

    private static bool TryDirectory(
        ReadOnlySpan<char> directory,
        ReadOnlySpan<char> executable,
        string[] executableExtensions,
        [NotNullWhen(true)] out string? result)
    {
        return TryExecutablePath(
            Path.GetFullPath(Path.Join(directory, executable)),
            executableExtensions,
            out result);
    }

    private static bool TryExecutablePath(
        string path,
        string[] executableExtensions,
        [NotNullWhen(true)] out string? result)
    {
        result = null;

        if (OperatingSystem.IsWindows())
        {
            var extension = Path.GetExtension(path);

            if (extension.Length > 0)
            {
                if (!executableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
                    !File.Exists(path))
                {
                    return false;
                }

                result = path;
                return true;
            }

            foreach (var executableExtension in executableExtensions)
            {
                var executablePath = string.Concat(path, executableExtension);

                if (File.Exists(executablePath))
                {
                    result = executablePath;
                    return true;
                }
            }

            return false;
        }

        if (!File.Exists(path))
            return false;

        var mode = File.GetUnixFileMode(path);
        const UnixFileMode executableModes = UnixFileMode.UserExecute |
                                             UnixFileMode.GroupExecute |
                                             UnixFileMode.OtherExecute;

        if ((mode & executableModes) == 0)
            return false;

        result = path;
        return true;
    }

    private static string[] GetWindowsExecutableExtensions()
    {
        string[] defaultExecutableExtensions = [".COM", ".EXE", ".BAT", ".CMD"];
        var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT");

        if (string.IsNullOrWhiteSpace(pathExtensions))
            return defaultExecutableExtensions;

        var executableExtensions = pathExtensions
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static extension => extension.StartsWith('.') ? extension : string.Concat('.', extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return executableExtensions.Length > 0 ? executableExtensions : defaultExecutableExtensions;
    }
}
