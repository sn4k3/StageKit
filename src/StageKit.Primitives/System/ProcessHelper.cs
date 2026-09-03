using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using StageKit.Primitives.Extensions;

namespace StageKit.Primitives.System;

/// <summary>
/// Provides cross-platform process launching, command-shell, output-capture, and administrator-elevation helpers.
/// </summary>
public static class ProcessHelper
{
    private static readonly bool IsFlatpakSandbox = OperatingSystem.IsLinux() &&
                                                     !string.IsNullOrWhiteSpace(
                                                         Environment.GetEnvironmentVariable("FLATPAK_ID"));

    #region Elevation exit codes

    /// <summary>
    /// The exit code reported when a Windows <c>runas</c> elevation prompt is cancelled.
    /// </summary>
    /// <remarks>
    /// Windows surfaces the cancellation as <c>ERROR_CANCELLED</c> from process creation rather than as a process
    /// exit code. The start helpers translate it to this value so callers can recognize it like any other code.
    /// </remarks>
    public const int WindowsElevationCancelledExitCode = 1223;

    /// <summary>
    /// The exit code <c>pkexec</c> reports when the Linux authentication dialog is dismissed.
    /// </summary>
    public const int LinuxElevationDismissedExitCode = 126;

    /// <summary>
    /// The exit code <c>pkexec</c> reports when Linux authorization could not be obtained.
    /// </summary>
    /// <remarks>
    /// <c>pkexec</c> also uses this value for general errors, and a shell uses it for a command that was not found,
    /// so it is not an unambiguous denial signal.
    /// </remarks>
    public const int LinuxElevationNotAuthorizedExitCode = 127;

    /// <summary>
    /// The exit code <c>osascript</c> reports when a macOS administrator prompt is cancelled.
    /// </summary>
    /// <remarks>
    /// This value is indistinguishable from an ordinary command failure. Use
    /// <see cref="IsExitCodeElevationDenied(int, string?)"/>, which also inspects the standard error, to classify a
    /// macOS result.
    /// </remarks>
    public const int MacOSElevationCancelledExitCode = 1;

    /// <summary>
    /// The AppleScript error number reported when a user cancels a macOS administrator prompt.
    /// </summary>
    private const string MacOSUserCancelledErrorNumber = "-128";

    /// <summary>
    /// Determines whether an exit code reports that an administrator elevation request was denied.
    /// </summary>
    /// <param name="exitCode">The exit code returned by a start or output helper.</param>
    /// <returns><see langword="true"/> when the exit code reports a denied elevation request.</returns>
    /// <remarks>
    /// Always returns <see langword="false"/> on macOS, where a cancelled prompt and a failed command share
    /// <see cref="MacOSElevationCancelledExitCode"/>. Use <see cref="IsExitCodeElevationDenied(int, string?)"/> to
    /// classify a macOS result.
    /// </remarks>
    public static bool IsExitCodeElevationDenied(int exitCode)
    {
        if (OperatingSystem.IsWindows()) return exitCode == WindowsElevationCancelledExitCode;

        if (OperatingSystem.IsLinux())
            return exitCode is LinuxElevationDismissedExitCode or LinuxElevationNotAuthorizedExitCode;

        return false;
    }

    /// <summary>
    /// Determines whether a captured process result reports that an administrator elevation request was denied.
    /// </summary>
    /// <param name="exitCode">The exit code returned by a start or output helper.</param>
    /// <param name="standardError">The captured standard error, when available.</param>
    /// <returns><see langword="true"/> when the result reports a denied elevation request.</returns>
    /// <remarks>
    /// Adds the macOS case that <see cref="IsExitCodeElevationDenied(int)"/> cannot decide, by matching the
    /// AppleScript cancellation error <c>-128</c> that <c>osascript</c> writes to standard error.
    /// </remarks>
    public static bool IsExitCodeElevationDenied(int exitCode, string? standardError)
    {
        if (IsExitCodeElevationDenied(exitCode)) return true;

        return OperatingSystem.IsMacOS() &&
               exitCode == MacOSElevationCancelledExitCode &&
               !string.IsNullOrEmpty(standardError) &&
               standardError.Contains(MacOSUserCancelledErrorNumber, StringComparison.Ordinal);
    }

    #endregion

    /// <summary>
    /// Starts a process with the given name and arguments.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation, unless the current process is already privileged,
    /// using <c>runas</c> on Windows, <c>pkexec</c> on Linux, or <c>osascript</c> on macOS.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <returns>
    /// The exit code when waiting for completion, zero when the process starts without waiting, or <c>-1</c> when
    /// startup fails or the wait times out.
    /// </returns>
    public static int StartProcess(
        string name,
        string? arguments = null,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite)
    {
        try
        {
            return StartProcess(CreateProcessStartInfo(name, arguments, requireElevation), waitForCompletion,
                waitTimeout);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Asynchronously starts a process with the given name and arguments.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation, unless the current process is already privileged,
    /// using <c>runas</c> on Windows, <c>pkexec</c> on Linux, or <c>osascript</c> on macOS.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to asynchronously wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <param name="cancellationToken">The token used to cancel waiting for completion.</param>
    /// <returns>
    /// A task containing the exit code when waiting for completion, zero when the process starts without waiting, or
    /// <c>-1</c> when startup fails or the wait times out.
    /// </returns>
    public static async Task<int> StartProcessAsync(
        string name,
        string? arguments = null,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await StartProcessAsync(
                    CreateProcessStartInfo(name, arguments, requireElevation),
                    waitForCompletion,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Starts a process with the given name and argument list.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation, unless the current process is already privileged,
    /// using <c>runas</c> on Windows, <c>pkexec</c> on Linux, or <c>osascript</c> on macOS.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <returns>
    /// The exit code when waiting for completion, zero when the process starts without waiting, or <c>-1</c> when
    /// startup fails or the wait times out.
    /// </returns>
    public static int StartProcess(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite)
    {
        try
        {
            return StartProcess(CreateProcessStartInfo(name, arguments, requireElevation), waitForCompletion,
                waitTimeout);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Asynchronously starts a process with the given name and argument list.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation, unless the current process is already privileged,
    /// using <c>runas</c> on Windows, <c>pkexec</c> on Linux, or <c>osascript</c> on macOS.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to asynchronously wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <param name="cancellationToken">The token used to cancel waiting for completion.</param>
    /// <returns>
    /// A task containing the exit code when waiting for completion, zero when the process starts without waiting, or
    /// <c>-1</c> when startup fails or the wait times out.
    /// </returns>
    public static async Task<int> StartProcessAsync(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await StartProcessAsync(
                    CreateProcessStartInfo(name, arguments, requireElevation),
                    waitForCompletion,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Starts a process on the host system, escaping a Flatpak sandbox when necessary.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the host process is already privileged.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <returns>
    /// The exit code when waiting for completion, zero when the process starts without waiting, or <c>-1</c> when
    /// startup fails or the wait times out.
    /// </returns>
    /// <remarks>
    /// Inside a Flatpak sandbox, the command is launched through <c>flatpak-spawn --host</c>. Outside Flatpak, this
    /// method behaves like <see cref="StartProcess(string, IEnumerable{string}, bool, bool, int)"/>.
    /// </remarks>
    public static int StartHostProcess(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite)
    {
        try
        {
            return StartProcess(CreateHostProcessStartInfo(name, arguments, requireElevation), waitForCompletion,
                waitTimeout);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Asynchronously starts a process on the host system, escaping a Flatpak sandbox when necessary.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the host process is already privileged.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to asynchronously wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <param name="cancellationToken">The token used to cancel waiting for completion.</param>
    /// <returns>
    /// A task containing the exit code when waiting for completion, zero when the process starts without waiting, or
    /// <c>-1</c> when startup fails or the wait times out.
    /// </returns>
    /// <remarks>
    /// Inside a Flatpak sandbox, the command is launched through <c>flatpak-spawn --host</c>. Outside Flatpak, this
    /// method behaves like <see cref="StartProcessAsync(string, IEnumerable{string}, bool, bool, int, CancellationToken)"/>.
    /// </remarks>
    public static async Task<int> StartHostProcessAsync(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await StartProcessAsync(
                    CreateHostProcessStartInfo(name, arguments, requireElevation),
                    waitForCompletion,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Starts a process using the supplied start information.
    /// </summary>
    /// <param name="processStartInfo">
    /// The process configuration, including its working directory, environment, and window settings.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <returns>
    /// The exit code when waiting for completion, zero when the process starts without waiting, or <c>-1</c> when
    /// startup fails or the wait times out.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="processStartInfo"/> is <see langword="null"/>.</exception>
    public static int StartProcess(
        ProcessStartInfo processStartInfo,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);

        try
        {
            return StartProcessCore(processStartInfo, waitForCompletion, waitTimeout);
        }
        catch (Exception exception) when (IsWindowsElevationCancelled(exception))
        {
            Debug.WriteLine(exception);
            return WindowsElevationCancelledExitCode;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Asynchronously starts a process using the supplied start information.
    /// </summary>
    /// <param name="processStartInfo">
    /// The process configuration, including its working directory, environment, and window settings.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to asynchronously wait for the process to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <param name="cancellationToken">The token used to cancel waiting for completion.</param>
    /// <returns>
    /// A task containing the exit code when waiting for completion, zero when the process starts without waiting, or
    /// <c>-1</c> when startup fails or the wait times out.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="processStartInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public static async Task<int> StartProcessAsync(
        ProcessStartInfo processStartInfo,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);

        try
        {
            return await StartProcessCoreAsync(
                    processStartInfo,
                    waitForCompletion,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsWindowsElevationCancelled(exception))
        {
            Debug.WriteLine(exception);
            return WindowsElevationCancelledExitCode;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    /// <summary>
    /// Starts a command through the host command shell.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to wait for the shell to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <returns>
    /// The exit code when waiting for completion, zero when the shell starts without waiting, or <c>-1</c> when
    /// startup fails or the wait times out.
    /// </returns>
    /// <remarks>Uses <c>cmd /d /c</c> on Windows and <c>bash -c</c> on other operating systems.</remarks>
    public static int StartShell(
        string command,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite)
    {
        return StartProcess(
            CreateShellProcessStartInfo(command, requireElevation),
            waitForCompletion,
            waitTimeout);
    }

    /// <summary>
    /// Asynchronously starts a command through the host command shell.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <param name="waitForCompletion"><see langword="true"/> to asynchronously wait for the shell to complete.</param>
    /// <param name="waitTimeout">The number of milliseconds to wait for completion.</param>
    /// <param name="cancellationToken">The token used to cancel waiting for completion.</param>
    /// <returns>
    /// A task containing the exit code when waiting for completion, zero when the shell starts without waiting, or
    /// <c>-1</c> when startup fails or the wait times out.
    /// </returns>
    /// <remarks>Uses <c>cmd /d /c</c> on Windows and <c>bash -c</c> on other operating systems.</remarks>
    public static Task<int> StartShellAsync(
        string command,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite,
        CancellationToken cancellationToken = default)
    {
        return StartProcessAsync(
            CreateShellProcessStartInfo(command, requireElevation),
            waitForCompletion,
            waitTimeout,
            cancellationToken);
    }

    /// <summary>
    /// Runs a process to completion and captures its standard output and standard error.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>The process exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// A non-privileged Windows process cannot capture output when using <c>runas</c>; that combination returns a
    /// failed result with exit code <c>-1</c>.
    /// </remarks>
    public static ProcessOutput GetProcessOutput(
        string name,
        string? arguments = null,
        bool requireElevation = false)
    {
        try
        {
            return GetProcessOutput(CreateProcessStartInfo(name, arguments, requireElevation));
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(-1, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Asynchronously runs a process to completion and captures its standard output and standard error.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the process and output capture.</param>
    /// <returns>A task containing the process exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// A non-privileged Windows process cannot capture output when using <c>runas</c>; that combination returns a
    /// failed result with exit code <c>-1</c>.
    /// </remarks>
    public static async Task<ProcessOutput> GetProcessOutputAsync(
        string name,
        string? arguments = null,
        bool requireElevation = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetProcessOutputAsync(
                    CreateProcessStartInfo(name, arguments, requireElevation),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(-1, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Runs a process to completion and captures its standard output and standard error.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>The process exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// A non-privileged Windows process cannot capture output when using <c>runas</c>; that combination returns a
    /// failed result with exit code <c>-1</c>.
    /// </remarks>
    public static ProcessOutput GetProcessOutput(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false)
    {
        try
        {
            return GetProcessOutput(CreateProcessStartInfo(name, arguments, requireElevation));
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(-1, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Asynchronously runs a process to completion and captures its standard output and standard error.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the process and output capture.</param>
    /// <returns>A task containing the process exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// A non-privileged Windows process cannot capture output when using <c>runas</c>; that combination returns a
    /// failed result with exit code <c>-1</c>.
    /// </remarks>
    public static async Task<ProcessOutput> GetProcessOutputAsync(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetProcessOutputAsync(
                    CreateProcessStartInfo(name, arguments, requireElevation),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(-1, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Runs a process to completion using the supplied start information and captures its standard output and error.
    /// </summary>
    /// <param name="processStartInfo">
    /// The process configuration, including its working directory, environment, and window settings.
    /// </param>
    /// <returns>The process exit code, standard output, and standard error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="processStartInfo"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Output capture enables redirection and sets <see cref="ProcessStartInfo.UseShellExecute"/> to
    /// <see langword="false"/>. A non-privileged Windows process cannot capture output through <c>runas</c>; that
    /// combination returns a failed result with exit code <c>-1</c>.
    /// </remarks>
    public static ProcessOutput GetProcessOutput(ProcessStartInfo processStartInfo)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);

        try
        {
            return GetProcessOutputCore(processStartInfo);
        }
        catch (Exception exception) when (IsWindowsElevationCancelled(exception))
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(WindowsElevationCancelledExitCode, string.Empty, string.Empty);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(-1, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Asynchronously runs a process to completion using the supplied start information and captures its standard
    /// output and error.
    /// </summary>
    /// <param name="processStartInfo">
    /// The process configuration, including its working directory, environment, and window settings.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the process and output capture.</param>
    /// <returns>A task containing the process exit code, standard output, and standard error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="processStartInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <remarks>
    /// Output capture enables redirection and sets <see cref="ProcessStartInfo.UseShellExecute"/> to
    /// <see langword="false"/>. A non-privileged Windows process cannot capture output through <c>runas</c>; that
    /// combination returns a failed result with exit code <c>-1</c>.
    /// </remarks>
    public static async Task<ProcessOutput> GetProcessOutputAsync(
        ProcessStartInfo processStartInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);

        try
        {
            return await GetProcessOutputCoreAsync(processStartInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsWindowsElevationCancelled(exception))
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(WindowsElevationCancelledExitCode, string.Empty, string.Empty);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ProcessOutput(-1, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Runs a command through the host command shell and captures its standard output and standard error.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>The shell exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// Uses <c>cmd /d /c</c> on Windows and <c>bash -c</c> on other operating systems. A non-privileged Windows process
    /// cannot capture output when using <c>runas</c>; that combination returns a failed result with exit code <c>-1</c>.
    /// </remarks>
    public static ProcessOutput GetShellOutput(string command, bool requireElevation = false)
    {
        return GetProcessOutput(CreateShellProcessStartInfo(command, requireElevation));
    }

    /// <summary>
    /// Asynchronously runs a command through the host command shell and captures its standard output and standard error.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the shell and output capture.</param>
    /// <returns>A task containing the shell exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// Uses <c>cmd /d /c</c> on Windows and <c>bash -c</c> on other operating systems. A non-privileged Windows process
    /// cannot capture output when using <c>runas</c>; that combination returns a failed result with exit code <c>-1</c>.
    /// </remarks>
    public static Task<ProcessOutput> GetShellOutputAsync(
        string command,
        bool requireElevation = false,
        CancellationToken cancellationToken = default)
    {
        return GetProcessOutputAsync(
            CreateShellProcessStartInfo(command, requireElevation),
            cancellationToken);
    }

    /// <summary>
    /// Creates start information for a command executed through the host command shell.
    /// </summary>
    /// <param name="argument">The shell command to execute.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
    /// <remarks>
    /// <p>Uses <c>cmd /d /c</c> on Windows and <c>bash -c</c> on other operating systems.</p>
    /// <p>
    /// When elevation is applied on Linux or macOS the returned instance targets the <c>pkexec</c> or
    /// <c>osascript</c> launcher rather than the command itself, so
    /// <see cref="ProcessStartInfo.WorkingDirectory"/>, <see cref="ProcessStartInfo.Environment"/>, and stream
    /// redirection configure the launcher and do not reach the elevated command. Commands that depend on those
    /// settings must establish them themselves.
    /// </p>
    /// </remarks>
    public static ProcessStartInfo CreateShellProcessStartInfo(
        string argument,
        bool requireElevation = false)
    {
        return CreateShellProcessStartInfo(argument, requireElevation, Environment.IsPrivilegedProcess);
    }

    internal static ProcessStartInfo CreateShellProcessStartInfo(
        string argument,
        bool requireElevation,
        bool isPrivilegedProcess)
    {
        return CreateShellProcessStartInfo([argument], requireElevation, isPrivilegedProcess);
    }

    /// <summary>
    /// Creates start information for the host command shell with the supplied argument list.
    /// </summary>
    /// <param name="arguments">The complete argument list to pass to the host command shell.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <p>
    /// Prepends <c>/d /c</c> for <c>cmd.exe</c> on Windows and <c>-c</c> for <c>bash</c> on other operating systems.
    /// </p>
    /// <p>
    /// When elevation is applied on Linux or macOS the returned instance targets the <c>pkexec</c> or
    /// <c>osascript</c> launcher rather than the command itself, so
    /// <see cref="ProcessStartInfo.WorkingDirectory"/>, <see cref="ProcessStartInfo.Environment"/>, and stream
    /// redirection configure the launcher and do not reach the elevated command.
    /// </p>
    /// </remarks>
    public static ProcessStartInfo CreateShellProcessStartInfo(
        IEnumerable<string> arguments,
        bool requireElevation = false)
    {
        return CreateShellProcessStartInfo(arguments, requireElevation, Environment.IsPrivilegedProcess);
    }

    internal static ProcessStartInfo CreateShellProcessStartInfo(
        IEnumerable<string> arguments,
        bool requireElevation,
        bool isPrivilegedProcess)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var (name, argumentPrefix) = GetShell();
        return CreateProcessStartInfo(
            name,
            [.. argumentPrefix, .. arguments],
            requireElevation,
            isPrivilegedProcess);
    }

    /// <summary>
    /// Creates start information that runs a script file through the host command shell.
    /// </summary>
    /// <param name="scriptFilePath">The path of the script file to run.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="scriptFilePath"/> is <see langword="null"/>, empty, or white space.
    /// </exception>
    /// <remarks>
    /// <p>
    /// Prefer this over <see cref="CreateShellProcessStartInfo(string, bool)"/> when launching a script file. The
    /// path is passed as a discrete argument rather than inside a shell command string, so a path containing spaces
    /// survives. <c>bash -c</c> word-splits its command string and would break such a path.
    /// </p>
    /// <p>
    /// Runs the script with <c>bash</c> on Unix, which reads the file directly and ignores its shebang, and through
    /// <c>cmd /d /c</c> on Windows, which cannot launch a batch file without a command interpreter.
    /// </p>
    /// <p>
    /// When elevation is applied on Linux or macOS the returned instance targets the <c>pkexec</c> or
    /// <c>osascript</c> launcher rather than the script, so
    /// <see cref="ProcessStartInfo.WorkingDirectory"/>, <see cref="ProcessStartInfo.Environment"/>, and stream
    /// redirection configure the launcher and do not reach the script.
    /// </p>
    /// </remarks>
    public static ProcessStartInfo CreateShellScriptProcessStartInfo(
        string scriptFilePath,
        bool requireElevation = false)
    {
        return CreateShellScriptProcessStartInfo(scriptFilePath, requireElevation, Environment.IsPrivilegedProcess);
    }

    internal static ProcessStartInfo CreateShellScriptProcessStartInfo(
        string scriptFilePath,
        bool requireElevation,
        bool isPrivilegedProcess)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptFilePath);

        var (name, argumentPrefix) = GetShell();

        // Unix shells word-split the "-c" command string, so hand the path over as its own argument instead.
        string[] arguments = OperatingSystem.IsWindows()
            ? [.. argumentPrefix, scriptFilePath]
            : [scriptFilePath];

        return CreateProcessStartInfo(name, arguments, requireElevation, isPrivilegedProcess);
    }

    /// <summary>
    /// Creates start information for a process with raw command-line arguments and optional administrator elevation.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The raw arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
    /// <remarks>
    /// When elevation is applied on Linux or macOS the returned instance targets the <c>pkexec</c> or
    /// <c>osascript</c> launcher rather than <paramref name="name"/>, so
    /// <see cref="ProcessStartInfo.WorkingDirectory"/>, <see cref="ProcessStartInfo.Environment"/>, and stream
    /// redirection configure the launcher and do not reach the elevated command.
    /// </remarks>
    public static ProcessStartInfo CreateProcessStartInfo(
        string name,
        string? arguments = null,
        bool requireElevation = false)
    {
        return CreateProcessStartInfo(name, arguments, requireElevation, Environment.IsPrivilegedProcess);
    }

    internal static ProcessStartInfo CreateProcessStartInfo(
        string name,
        string? arguments,
        bool requireElevation,
        bool isPrivilegedProcess)
    {
        if (!requireElevation || isPrivilegedProcess)
            return new ProcessStartInfo(name, arguments ?? string.Empty);

        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo(name, arguments ?? string.Empty)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
        }

        if (OperatingSystem.IsLinux())
            return new ProcessStartInfo("pkexec", JoinProcessArguments(name, arguments));

        if (OperatingSystem.IsMacOS())
        {
            var command = string.IsNullOrWhiteSpace(arguments)
                ? name.QuoteShell()
                : string.Concat(name.QuoteShell(), " ", arguments);

            return CreateMacOSElevatedProcessStartInfo(command);
        }

        throw new PlatformNotSupportedException(
            "Elevated process launching is not supported on this operating system.");
    }

    /// <summary>
    /// Creates start information for a process with an argument list and optional administrator elevation.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// When elevation is applied on Linux or macOS the returned instance targets the <c>pkexec</c> or
    /// <c>osascript</c> launcher rather than <paramref name="name"/>, so
    /// <see cref="ProcessStartInfo.WorkingDirectory"/>, <see cref="ProcessStartInfo.Environment"/>, and stream
    /// redirection configure the launcher and do not reach the elevated command.
    /// </remarks>
    public static ProcessStartInfo CreateProcessStartInfo(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false)
    {
        return CreateProcessStartInfo(name, arguments, requireElevation, Environment.IsPrivilegedProcess);
    }

    /// <summary>
    /// Creates start information for a process on the host system, escaping a Flatpak sandbox when necessary.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the host process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Inside a Flatpak sandbox, the returned instance targets <c>flatpak-spawn --host</c>. Outside Flatpak, this
    /// method behaves like <see cref="CreateProcessStartInfo(string, IEnumerable{string}, bool)"/>.
    /// </remarks>
    public static ProcessStartInfo CreateHostProcessStartInfo(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false)
    {
        return CreateHostProcessStartInfo(
            name,
            arguments,
            requireElevation,
            IsFlatpakSandbox,
            Environment.IsPrivilegedProcess);
    }

    internal static ProcessStartInfo CreateHostProcessStartInfo(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation,
        bool isFlatpakSandbox,
        bool isPrivilegedProcess)
    {
        var targetStartInfo = CreateProcessStartInfo(name, arguments, requireElevation, isPrivilegedProcess);
        if (!isFlatpakSandbox) return targetStartInfo;

        var hostStartInfo = CreateArgumentListProcessStartInfo(
            "flatpak-spawn",
            ["--host", targetStartInfo.FileName]);

        foreach (var argument in targetStartInfo.ArgumentList)
        {
            hostStartInfo.ArgumentList.Add(argument);
        }

        return hostStartInfo;
    }

    internal static ProcessStartInfo CreateProcessStartInfo(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation,
        bool isPrivilegedProcess)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!requireElevation || isPrivilegedProcess)
            return CreateArgumentListProcessStartInfo(name, arguments);

        if (OperatingSystem.IsWindows())
        {
            var processStartInfo = CreateArgumentListProcessStartInfo(name, arguments);
            processStartInfo.UseShellExecute = true;
            processStartInfo.Verb = "runas";
            return processStartInfo;
        }

        if (OperatingSystem.IsLinux())
        {
            var processStartInfo = CreateArgumentListProcessStartInfo("pkexec", [name]);

            foreach (var argument in arguments)
            {
                processStartInfo.ArgumentList.Add(argument);
            }

            return processStartInfo;
        }

        if (OperatingSystem.IsMacOS())
        {
            var command = new StringBuilder(name.QuoteShell());

            foreach (var argument in arguments)
            {
                command.Append(' ').Append(argument.QuoteShell());
            }

            return CreateMacOSElevatedProcessStartInfo(command.ToString());
        }

        throw new PlatformNotSupportedException(
            "Elevated process launching is not supported on this operating system.");
    }

    private static ProcessOutput GetProcessOutputCore(ProcessStartInfo processStartInfo)
    {
        ConfigureOutputCapture(processStartInfo);

        using var process = Process.Start(processStartInfo);

        if (process is null)
            return new ProcessOutput(-1, string.Empty, string.Empty);

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        return new ProcessOutput(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    private static async Task<ProcessOutput> GetProcessOutputCoreAsync(
        ProcessStartInfo processStartInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConfigureOutputCapture(processStartInfo);

        using var process = Process.Start(processStartInfo);

        if (process is null)
            return new ProcessOutput(-1, string.Empty, string.Empty);

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new ProcessOutput(
                process.ExitCode,
                await standardOutput.ConfigureAwait(false),
                await standardError.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw;
        }
    }

    private static int StartProcessCore(
        ProcessStartInfo processStartInfo,
        bool waitForCompletion,
        int waitTimeout)
    {
        using var process = Process.Start(processStartInfo);

        if (process is null)
            return -1;

        if (!waitForCompletion)
            return 0;

        return process.WaitForExit(waitTimeout) ? process.ExitCode : -1;
    }

    private static async Task<int> StartProcessCoreAsync(
        ProcessStartInfo processStartInfo,
        bool waitForCompletion,
        int waitTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = Process.Start(processStartInfo);

        if (process is null)
            return -1;

        if (!waitForCompletion)
            return 0;

        if (waitTimeout == Timeout.Infinite)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }

        using var timeoutCancellationSource = new CancellationTokenSource(waitTimeout);
        using var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellationSource.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return -1;
        }
    }

    private static ProcessStartInfo CreateArgumentListProcessStartInfo(
        string name,
        IEnumerable<string> arguments)
    {
        var processStartInfo = new ProcessStartInfo(name);

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        return processStartInfo;
    }

    internal static void ConfigureOutputCapture(ProcessStartInfo processStartInfo)
    {
        if (OperatingSystem.IsWindows() &&
            string.Equals(processStartInfo.Verb, "runas", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Standard output and error cannot be captured from a Windows runas process.");
        }

        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;
        processStartInfo.UseShellExecute = false;
    }

    private static ProcessStartInfo CreateMacOSElevatedProcessStartInfo(string command)
    {
        var processStartInfo = CreateArgumentListProcessStartInfo("osascript", ["-e"]);
        processStartInfo.ArgumentList.Add(
            $"do shell script \"{EscapeAppleScriptString(command)}\" with administrator privileges");
        return processStartInfo;
    }

    private static string JoinProcessArguments(string name, string? arguments)
    {
        return string.IsNullOrWhiteSpace(arguments)
            ? name.QuoteProcessArgument()
            : string.Concat(name.QuoteProcessArgument(), " ", arguments);
    }

    /// <summary>
    /// Determines whether an exception reports a cancelled Windows elevation prompt.
    /// </summary>
    /// <param name="exception">The exception raised while starting the process.</param>
    /// <returns><see langword="true"/> when the user dismissed the <c>runas</c> prompt.</returns>
    /// <remarks>
    /// Windows reports the dismissal as <c>ERROR_CANCELLED</c> from process creation, so it never reaches the
    /// caller as an exit code without this translation.
    /// </remarks>
    private static bool IsWindowsElevationCancelled(Exception exception)
    {
        return OperatingSystem.IsWindows() &&
               exception is Win32Exception { NativeErrorCode: WindowsElevationCancelledExitCode };
    }

    private static string EscapeAppleScriptString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                              global::System.ComponentModel.Win32Exception or
                                              NotSupportedException)
        {
            Debug.WriteLine(exception);
        }
    }

    private static (string Name, string[] ArgumentPrefix) GetShell()
    {
        return OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/d", "/c"])
            : ("bash", ["-c"]);
    }
}
