using System.Diagnostics;
using System.Text;
using StageKit.Primitives.Extensions;

namespace StageKit.Primitives.System;

/// <summary>
/// Provides cross-platform process launching, command-shell, output-capture, and administrator-elevation helpers.
/// </summary>
public static class ProcessHelper
{
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
    /// <remarks>Uses <c>cmd /d /c</c> on Windows and <c>bash -c</c> on other operating systems.</remarks>
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
    /// Prepends <c>/d /c</c> for <c>cmd.exe</c> on Windows and <c>-c</c> for <c>bash</c> on other operating systems.
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
    /// Creates start information for a process with raw command-line arguments and optional administrator elevation.
    /// </summary>
    /// <param name="name">The executable name or path.</param>
    /// <param name="arguments">The raw arguments to pass to the process.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>A configurable <see cref="ProcessStartInfo"/> instance.</returns>
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
    public static ProcessStartInfo CreateProcessStartInfo(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation = false)
    {
        return CreateProcessStartInfo(name, arguments, requireElevation, Environment.IsPrivilegedProcess);
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