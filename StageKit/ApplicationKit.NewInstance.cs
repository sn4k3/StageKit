using StageKit.Primitives;
using StageKit.Runtime;

namespace StageKit;

public static partial class ApplicationKit
{
    /// <summary>
    /// Launches a new application instance while preserving the current application arguments.
    /// </summary>
    /// <param name="runArguments">Additional arguments to append after the preserved application arguments.</param>
    /// <returns><see langword="true"/> if a new instance was launched; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Arguments from <see cref="ApplicationArgs"/> are preserved except for the initial executable path and any
    /// existing <see cref="CrashReportFlag"/> argument with its following crash report identifier.
    /// </remarks>
    public static bool LaunchNewInstanceKeepApplicationArgs(params string[] runArguments)
    {
        return EntryApplication.LaunchNewInstance(GetLaunchArgumentsKeepApplicationArgs(runArguments));
    }

    /// <summary>
    /// Launches a new application instance while preserving the current application arguments.
    /// </summary>
    /// <param name="runArguments">Additional arguments to append after the preserved application arguments.</param>
    /// <returns><see langword="true"/> if a new instance was launched; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Arguments from <see cref="ApplicationArgs"/> are preserved except for the initial executable path and any
    /// existing <see cref="CrashReportFlag"/> argument with its following crash report identifier.
    /// </remarks>
    public static bool LaunchNewInstanceKeepApplicationArgs(IEnumerable<string> runArguments)
    {
        return EntryApplication.LaunchNewInstance(GetLaunchArgumentsKeepApplicationArgs(runArguments));
    }

    /// <summary>
    /// Gets the launch arguments for a new application instance while preserving the current application arguments.
    /// </summary>
    /// <param name="runArguments">Additional arguments to append after the preserved application arguments.</param>
    /// <returns>The combined launch arguments.</returns>
    public static string[] GetLaunchArgumentsKeepApplicationArgs(params string[] runArguments)
    {
        if (ApplicationArgs is not { Length: > 0 } applicationArgs) return runArguments;

        var preservedArguments = new List<string>(applicationArgs.Length + runArguments.Length);

        for (var i = 0; i < applicationArgs.Length; i++)
        {
            var argument = applicationArgs[i];
            if (i == 0)
            {
                if (PathUtilities.IsSamePath(argument, EntryApplication.ProcessPath)) continue;
                if (PathUtilities.IsSamePath(argument, EntryApplication.ExecutablePath)) continue;
            }

            if (!string.IsNullOrWhiteSpace(CrashReportFlag) &&
                string.Equals(argument, CrashReportFlag, StringComparison.InvariantCultureIgnoreCase))
            {
                if (i + 1 < applicationArgs.Length) i++;
                continue;
            }

            preservedArguments.Add(argument);
        }

        preservedArguments.AddRange(runArguments);
        return preservedArguments.ToArray();
    }

    /// <summary>
    /// Gets the launch arguments for a new application instance while preserving the current application arguments.
    /// </summary>
    /// <param name="runArguments">Additional arguments to append after the preserved application arguments.</param>
    /// <returns>The combined launch arguments.</returns>
    public static string[] GetLaunchArgumentsKeepApplicationArgs(IEnumerable<string> runArguments)
    {
        var arguments = runArguments as string[] ?? runArguments.ToArray();
        return GetLaunchArgumentsKeepApplicationArgs(arguments);
    }
}