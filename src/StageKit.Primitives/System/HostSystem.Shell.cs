using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
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
    /// Opens an absolute URL with the host's default application.
    /// </summary>
    /// <param name="url">The absolute, non-file URL to open.</param>
    /// <returns>
    /// <see langword="true"/> if the open request was started; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
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
    /// Asynchronously opens an absolute URL with the host's default application.
    /// </summary>
    /// <param name="url">The absolute, non-file URL to open.</param>
    /// <param name="cancellationToken">The token used to cancel the open request before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the open request was started; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static Task<bool> OpenUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return StartAsync(CreateUrlStartInfo(url), cancellationToken);
    }

    /// <summary>
    /// Opens a directory, file, URL, or other host-recognized target with its default application.
    /// </summary>
    /// <param name="target">The directory, file, URL, or raw host target to open.</param>
    /// <returns>
    /// <see langword="true"/> if the open request was started; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Open(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        return Start(CreateClassifiedOpenStartInfo(target)) || Start(CreateRawShellExecuteStartInfo(target));
    }

    /// <summary>
    /// Asynchronously opens a directory, file, URL, or other host-recognized target with its default application.
    /// </summary>
    /// <param name="target">The directory, file, URL, or raw host target to open.</param>
    /// <param name="cancellationToken">The token used to cancel the open request before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the open request was started; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static async Task<bool> OpenAsync(string target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(target))
            return false;

        if (await StartAsync(CreateClassifiedOpenStartInfo(target), cancellationToken).ConfigureAwait(false))
            return true;

        return await StartAsync(CreateRawShellExecuteStartInfo(target), cancellationToken).ConfigureAwait(false);
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
        return Start(CreateExistingPathStartInfo(directoryPath, true));
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
        return StartAsync(CreateExistingPathStartInfo(directoryPath, true), cancellationToken);
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
        return Start(CreateExistingPathStartInfo(filePath, false));
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
        return StartAsync(CreateExistingPathStartInfo(filePath, false), cancellationToken);
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
    /// Opens an interactive terminal window at the specified working directory, or the current directory if omitted.
    /// </summary>
    /// <param name="workingDirectory">The initial working directory, or <see langword="null"/> to use the current directory.</param>
    /// <returns><see langword="true"/> if the terminal window was started; otherwise, <see langword="false"/>.</returns>
    public static bool OpenTerminal(string? workingDirectory = null)
    {
        return Start(CreateOpenTerminalStartInfo(workingDirectory));
    }

    /// <summary>
    /// Asynchronously opens an interactive terminal window at the specified working directory, or the current directory if omitted.
    /// </summary>
    /// <param name="workingDirectory">The initial working directory, or <see langword="null"/> to use the current directory.</param>
    /// <param name="cancellationToken">The token used to cancel the request before it starts.</param>
    /// <returns>A task containing <see langword="true"/> if the terminal window was started; otherwise, <see langword="false"/>.</returns>
    public static Task<bool> OpenTerminalAsync(string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        return StartAsync(CreateOpenTerminalStartInfo(workingDirectory), cancellationToken);
    }

    /// <summary>
    /// Opens an interactive terminal window and runs the specified command or script.
    /// </summary>
    /// <param name="command">The command line to run in the terminal.</param>
    /// <param name="workingDirectory">The initial working directory, or <see langword="null"/> to use the current directory.</param>
    /// <param name="keepOpen"><see langword="true"/> to keep the terminal window open after the command exits; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the terminal window was started; otherwise, <see langword="false"/>.</returns>
    public static bool OpenInTerminal(string command, string? workingDirectory = null, bool keepOpen = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return Start(CreateOpenInTerminalStartInfo(command, workingDirectory, keepOpen));
    }

    /// <summary>
    /// Asynchronously opens an interactive terminal window and runs the specified command or script.
    /// </summary>
    /// <param name="command">The command line to run in the terminal.</param>
    /// <param name="workingDirectory">The initial working directory, or <see langword="null"/> to use the current directory.</param>
    /// <param name="keepOpen"><see langword="true"/> to keep the terminal window open after the command exits; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">The token used to cancel the request before it starts.</param>
    /// <returns>A task containing <see langword="true"/> if the terminal window was started; otherwise, <see langword="false"/>.</returns>
    public static Task<bool> OpenInTerminalAsync(
        string command,
        string? workingDirectory = null,
        bool keepOpen = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return StartAsync(CreateOpenInTerminalStartInfo(command, workingDirectory, keepOpen), cancellationToken);
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

    internal static ProcessStartInfo CreateRawShellExecuteStartInfo(string target)
    {
        return new ProcessStartInfo(target)
        {
            UseShellExecute = true
        };
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

    internal static ProcessStartInfo? CreateOpenTerminalStartInfo(string? workingDirectory = null)
    {
        string? resolvedDir = null;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            if (!Directory.Exists(workingDirectory))
                return null;

            try
            {
                resolvedDir = Path.GetFullPath(workingDirectory);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            if (TryFindExecutable("wt.exe", out var wtPath))
            {
                var psi = new ProcessStartInfo(wtPath) { UseShellExecute = true };
                if (resolvedDir is not null)
                    psi.Arguments = $"-d \"{resolvedDir}\"";
                return psi;
            }

            var cmdPsi = new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
            if (resolvedDir is not null)
            {
                cmdPsi.WorkingDirectory = resolvedDir;
                cmdPsi.Arguments = $"/k \"cd /d \"\"{resolvedDir}\"\"\"";
            }
            return cmdPsi;
        }

        if (OperatingSystem.IsMacOS())
        {
            return resolvedDir is not null
                ? CreateLauncherStartInfo("/usr/bin/open", "-a", "Terminal", resolvedDir)
                : CreateLauncherStartInfo("/usr/bin/open", "-a", "Terminal");
        }

        if (OperatingSystem.IsLinux())
        {
            var terminal = ResolveLinuxTerminalExecutable();
            if (terminal is null) return null;

            var fileName = Path.GetFileName(terminal);
            if (fileName.Equals("gnome-terminal", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("xfce4-terminal", StringComparison.OrdinalIgnoreCase))
            {
                return resolvedDir is not null
                    ? CreateLauncherStartInfo(terminal, $"--working-directory={resolvedDir}")
                    : CreateLauncherStartInfo(terminal);
            }

            if (fileName.Equals("konsole", StringComparison.OrdinalIgnoreCase))
            {
                return resolvedDir is not null
                    ? CreateLauncherStartInfo(terminal, "--workdir", resolvedDir)
                    : CreateLauncherStartInfo(terminal);
            }

            var startInfo = CreateLauncherStartInfo(terminal);
            if (resolvedDir is not null)
                startInfo.WorkingDirectory = resolvedDir;
            return startInfo;
        }

        return null;
    }

    internal static ProcessStartInfo? CreateOpenInTerminalStartInfo(
        string command,
        string? workingDirectory = null,
        bool keepOpen = true)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        string? resolvedDir = null;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            if (!Directory.Exists(workingDirectory))
                return null;

            try
            {
                resolvedDir = Path.GetFullPath(workingDirectory);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var flag = keepOpen ? "/k" : "/c";
            if (TryFindExecutable("wt.exe", out var wtPath))
            {
                var dirArg = resolvedDir is not null ? $"-d \"{resolvedDir}\" " : string.Empty;
                return new ProcessStartInfo(wtPath, $"{dirArg}cmd.exe {flag} {command}") { UseShellExecute = true };
            }

            var psi = new ProcessStartInfo("cmd.exe", $"{flag} {command}") { UseShellExecute = true };
            if (resolvedDir is not null)
                psi.WorkingDirectory = resolvedDir;
            return psi;
        }

        if (OperatingSystem.IsMacOS())
        {
            var fullCommand = resolvedDir is not null
                ? $"cd '{resolvedDir.Replace("'", "'\\''")}' && {command}"
                : command;
            if (!keepOpen)
                fullCommand += "; exit";

            var escaped = fullCommand.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return CreateLauncherStartInfo("/usr/bin/osascript",
                "-e", $"tell application \"Terminal\" to do script \"{escaped}\"",
                "-e", "tell application \"Terminal\" to activate");
        }

        if (OperatingSystem.IsLinux())
        {
            var terminal = ResolveLinuxTerminalExecutable();
            if (terminal is null) return null;

            var fileName = Path.GetFileName(terminal);
            var shellCmd = keepOpen ? $"{command}; exec bash" : command;

            if (fileName.Equals("gnome-terminal", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("xfce4-terminal", StringComparison.OrdinalIgnoreCase))
            {
                return resolvedDir is not null
                    ? CreateLauncherStartInfo(terminal, $"--working-directory={resolvedDir}", "--", "bash", "-c", shellCmd)
                    : CreateLauncherStartInfo(terminal, "--", "bash", "-c", shellCmd);
            }

            if (fileName.Equals("konsole", StringComparison.OrdinalIgnoreCase))
            {
                return resolvedDir is not null
                    ? CreateLauncherStartInfo(terminal, "--workdir", resolvedDir, "-e", "bash", "-c", shellCmd)
                    : CreateLauncherStartInfo(terminal, "-e", "bash", "-c", shellCmd);
            }

            var fallbackStartInfo = CreateLauncherStartInfo(terminal, "-e", $"bash -c \"{shellCmd}\"");
            if (resolvedDir is not null)
                fallbackStartInfo.WorkingDirectory = resolvedDir;
            return fallbackStartInfo;
        }

        return null;
    }

    private static string? ResolveLinuxTerminalExecutable()
    {
        ReadOnlySpan<string> candidates = ["x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm"];
        foreach (var candidate in candidates)
        {
            if (TryFindExecutable(candidate, out var found))
                return found;
        }
        return null;
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
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? CreateUrlStartInfo(uri) : null;
    }

    private static ProcessStartInfo? CreateUrlStartInfo(Uri uri)
    {
        return uri.IsAbsoluteUri && !uri.IsFile
            ? CreateOpenTargetStartInfo(uri.AbsoluteUri)
            : null;
    }

    internal static ProcessStartInfo? CreateClassifiedOpenStartInfo(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        if (Directory.Exists(target))
            return CreateExistingPathStartInfo(target, true);

        if (File.Exists(target))
            return CreateExistingPathStartInfo(target, false);

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return null;

        if (uri.IsFile)
        {
            try
            {
                var localPath = uri.LocalPath;
                return Directory.Exists(localPath)
                    ? CreateExistingPathStartInfo(localPath, true)
                    : File.Exists(localPath)
                        ? CreateExistingPathStartInfo(localPath, false)
                        : null;
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

        return CreateUrlStartInfo(uri);
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
