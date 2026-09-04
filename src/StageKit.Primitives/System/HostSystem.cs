using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

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
    /// Opens an absolute URL with the host's default application.
    /// </summary>
    /// <param name="url">The absolute URL to open.</param>
    /// <returns>
    /// <see langword="true"/> if the open request was started; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool OpenUrl(string url)
    {
        return Start(CreateUrlStartInfo(url));
    }

    /// <summary>
    /// Asynchronously opens an absolute URL with the host's default application.
    /// </summary>
    /// <param name="url">The absolute URL to open.</param>
    /// <param name="cancellationToken">The token used to cancel the open request before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the open request was started; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static Task<bool> OpenUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return StartAsync(CreateUrlStartInfo(url), cancellationToken);
    }

    /// <summary>
    /// Opens an existing directory in the host's default file manager.
    /// </summary>
    /// <param name="directoryPath">The directory to open.</param>
    /// <returns>
    /// <see langword="true"/> if the open request was started; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool OpenDirectory(string directoryPath)
    {
        return Start(CreateExistingPathStartInfo(directoryPath, isDirectory: true));
    }

    /// <summary>
    /// Asynchronously opens an existing directory in the host's default file manager.
    /// </summary>
    /// <param name="directoryPath">The directory to open.</param>
    /// <param name="cancellationToken">The token used to cancel the open request before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the open request was started; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static Task<bool> OpenDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return StartAsync(CreateExistingPathStartInfo(directoryPath, isDirectory: true), cancellationToken);
    }

    /// <summary>
    /// Opens an existing file with the host's default application.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <returns>
    /// <see langword="true"/> if the open request was started; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool OpenFile(string filePath)
    {
        return Start(CreateExistingPathStartInfo(filePath, isDirectory: false));
    }

    /// <summary>
    /// Asynchronously opens an existing file with the host's default application.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <param name="cancellationToken">The token used to cancel the open request before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the open request was started; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static Task<bool> OpenFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return StartAsync(CreateExistingPathStartInfo(filePath, isDirectory: false), cancellationToken);
    }

    /// <summary>
    /// Shows an existing file in the host's file manager.
    /// </summary>
    /// <param name="filePath">The file to show.</param>
    /// <returns>
    /// <see langword="true"/> if the request was started; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Windows Explorer and macOS Finder select the file. Linux opens the containing directory because desktop file
    /// managers do not provide one portable file-selection command.
    /// </remarks>
    public static bool ShowFileInFileManager(string filePath)
    {
        return Start(CreateShowExistingFileStartInfo(filePath));
    }

    /// <summary>
    /// Asynchronously shows an existing file in the host's file manager.
    /// </summary>
    /// <param name="filePath">The file to show.</param>
    /// <param name="cancellationToken">The token used to cancel the request before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the request was started; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Windows Explorer and macOS Finder select the file. Linux opens the containing directory because desktop file
    /// managers do not provide one portable file-selection command.
    /// </remarks>
    public static Task<bool> ShowFileInFileManagerAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return StartAsync(CreateShowExistingFileStartInfo(filePath), cancellationToken);
    }

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

    internal static ProcessStartInfo? CreateOpenTargetStartInfo(string target)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo(target)
            {
                UseShellExecute = true
            };
        }

        if (OperatingSystem.IsMacOS())
            return CreateLauncherStartInfo("/usr/bin/open", target);

        return OperatingSystem.IsLinux()
            ? CreateLauncherStartInfo("xdg-open", target)
            : null;
    }

    internal static ProcessStartInfo? CreateShowFileInFileManagerStartInfo(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return CreateLauncherStartInfo("explorer.exe", string.Concat("/select,", filePath));

        if (OperatingSystem.IsMacOS())
            return CreateLauncherStartInfo("/usr/bin/open", "-R", filePath);

        var directoryPath = Path.GetDirectoryName(filePath);
        return OperatingSystem.IsLinux() && directoryPath is not null
            ? CreateLauncherStartInfo("xdg-open", directoryPath)
            : null;
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

    private static ProcessStartInfo CreateLauncherStartInfo(string launcher, params ReadOnlySpan<string> arguments)
    {
        var startInfo = new ProcessStartInfo(launcher)
        {
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    private static ProcessStartInfo? CreateUrlStartInfo(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile
            ? CreateOpenTargetStartInfo(uri.AbsoluteUri)
            : null;
    }

    private static ProcessStartInfo? CreateExistingPathStartInfo(string path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            (isDirectory ? !Directory.Exists(path) : !File.Exists(path)))
        {
            return null;
        }

        try
        {
            return CreateOpenTargetStartInfo(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          IOException or
                                          UnauthorizedAccessException or
                                          NotSupportedException)
        {
            Debug.WriteLine(exception);
            return null;
        }
    }

    private static ProcessStartInfo? CreateShowExistingFileStartInfo(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            return CreateShowFileInFileManagerStartInfo(Path.GetFullPath(filePath));
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          IOException or
                                          UnauthorizedAccessException or
                                          NotSupportedException)
        {
            Debug.WriteLine(exception);
            return null;
        }
    }

    private static bool Start(ProcessStartInfo? startInfo)
    {
        return startInfo is not null && ProcessHelper.StartProcess(startInfo) == 0;
    }

    private static async Task<bool> StartAsync(
        ProcessStartInfo? startInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return startInfo is not null &&
               await ProcessHelper.StartProcessAsync(startInfo, cancellationToken: cancellationToken)
                   .ConfigureAwait(false) == 0;
    }
}
