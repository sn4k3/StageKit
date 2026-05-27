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
    public static int StartProcess(string name, string? arguments, bool waitForCompletion = false, int waitTimeout = Timeout.Infinite)
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
    public static int StartProcess(string name, IEnumerable<string> arguments, bool waitForCompletion = false, int waitTimeout = Timeout.Infinite)
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
}
