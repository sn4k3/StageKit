using StageKit.Primitives.Extensions;
using System.Diagnostics;
using System.Text;

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
            return StartProcess(CreateProcessStartInfo(name, arguments, requireElevation), waitForCompletion, waitTimeout);
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
            return StartProcess(CreateProcessStartInfo(name, arguments, requireElevation), waitForCompletion, waitTimeout);
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
    /// <remarks>Uses <c>cmd /c</c> on Windows and <c>bash -c</c> on other operating systems.</remarks>
    public static int StartShell(
        string command,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite)
    {
        var (name, commandSwitch) = GetShell();
        return StartProcess(
            name,
            [commandSwitch, command],
            requireElevation,
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
    /// <remarks>Uses <c>cmd /c</c> on Windows and <c>bash -c</c> on other operating systems.</remarks>
    public static Task<int> StartShellAsync(
        string command,
        bool requireElevation = false,
        bool waitForCompletion = false,
        int waitTimeout = Timeout.Infinite,
        CancellationToken cancellationToken = default)
    {
        var (name, commandSwitch) = GetShell();
        return StartProcessAsync(
            name,
            [commandSwitch, command],
            requireElevation,
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
    /// Runs a command through the host command shell and captures its standard output and standard error.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation unless the current process is already privileged.
    /// </param>
    /// <returns>The shell exit code, standard output, and standard error.</returns>
    /// <remarks>
    /// Uses <c>cmd /c</c> on Windows and <c>bash -c</c> on other operating systems. A non-privileged Windows process
    /// cannot capture output when using <c>runas</c>; that combination returns a failed result with exit code <c>-1</c>.
    /// </remarks>
    public static ProcessOutput GetShellOutput(string command, bool requireElevation = false)
    {
        var (name, commandSwitch) = GetShell();
        return GetProcessOutput(name, [commandSwitch, command], requireElevation);
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
    /// Uses <c>cmd /c</c> on Windows and <c>bash -c</c> on other operating systems. A non-privileged Windows process
    /// cannot capture output when using <c>runas</c>; that combination returns a failed result with exit code <c>-1</c>.
    /// </remarks>
    public static Task<ProcessOutput> GetShellOutputAsync(
        string command,
        bool requireElevation = false,
        CancellationToken cancellationToken = default)
    {
        var (name, commandSwitch) = GetShell();
        return GetProcessOutputAsync(name, [commandSwitch, command], requireElevation, cancellationToken);
    }

    internal static ProcessStartInfo CreateProcessStartInfo(
        string name,
        string? arguments,
        bool requireElevation)
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
        {
            return new ProcessStartInfo(name, arguments ?? string.Empty)
            {
                UseShellExecute = true
            };
        }

        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo(name, arguments ?? string.Empty)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return new ProcessStartInfo("pkexec", JoinProcessArguments(name, arguments))
            {
                UseShellExecute = true
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            var command = string.IsNullOrWhiteSpace(arguments)
                ? name.QuoteShell()
                : string.Concat(name.QuoteShell(), " ", arguments);

            return CreateMacOSElevatedProcessStartInfo(command);
        }

        throw new PlatformNotSupportedException("Elevated process launching is not supported on this operating system.");
    }

    internal static ProcessStartInfo CreateProcessStartInfo(
        string name,
        IEnumerable<string> arguments,
        bool requireElevation)
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

        throw new PlatformNotSupportedException("Elevated process launching is not supported on this operating system.");
    }

    private static ProcessOutput GetProcessOutput(ProcessStartInfo processStartInfo)
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

    private static async Task<ProcessOutput> GetProcessOutputAsync(
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

    private static int StartProcess(
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

    private static async Task<int> StartProcessAsync(
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
        var processStartInfo = new ProcessStartInfo(name)
        {
            UseShellExecute = true
        };

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
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          global::System.ComponentModel.Win32Exception or
                                          NotSupportedException)
        {
            Debug.WriteLine(exception);
        }
    }

    private static (string Name, string CommandSwitch) GetShell()
    {
        return OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c")
            : ("bash", "-c");
    }
}
