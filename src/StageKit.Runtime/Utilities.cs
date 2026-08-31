using System.Diagnostics;

namespace StageKit.Runtime;

/// <summary>
/// Provides utility methods and constants.
/// </summary>
internal static class Utilities
{
    /// <summary>
    /// Starts a process with the given name and arguments.
    /// </summary>
    /// <param name="name">The name of the process to start.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="waitForCompletion">True to wait for the process to complete.</param>
    /// <param name="waitTimeout">The timeout in milliseconds to wait for the process to complete.</param>
    /// <returns>The exit code of the process.</returns>
    internal static int StartProcess(string name, string? arguments, bool waitForCompletion = false, int waitTimeout = Timeout.Infinite)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(name, arguments ?? string.Empty) { UseShellExecute = true });
            if (process is null) return -1;
            if (waitForCompletion)
            {
                if (!process.WaitForExit(waitTimeout)) return -1;
                return process.ExitCode;
            }
            return 0;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return -1;
        }
    }

    /// <summary>
    /// Starts a process with the given name and argument list.
    /// </summary>
    /// <param name="name">The name of the process to start.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="waitForCompletion">True to wait for the process to complete.</param>
    /// <param name="waitTimeout">The timeout in milliseconds to wait for the process to complete.</param>
    /// <returns>The exit code when waiting for completion, 0 when the process starts successfully without waiting, or -1 when startup fails.</returns>
    internal static int StartProcess(string name, IEnumerable<string> arguments, bool waitForCompletion = false, int waitTimeout = Timeout.Infinite)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo(name)
            {
                UseShellExecute = true
            };

            foreach (var argument in arguments)
            {
                processStartInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(processStartInfo);
            if (process is null) return -1;
            if (waitForCompletion)
            {
                if (!process.WaitForExit(waitTimeout)) return -1;
                return process.ExitCode;
            }
            return 0;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return -1;
        }
    }

    internal static bool IsOwnedByPackage(string command, string argument, string file)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                ArgumentList =
                {
                    argument,
                    file
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
                return false;

            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            // Package manager doesn't exist, cannot be started, etc.
            return false;
        }
    }
}
