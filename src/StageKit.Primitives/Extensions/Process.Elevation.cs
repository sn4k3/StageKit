using System.Diagnostics;
using StageKit.Primitives.System;

namespace StageKit.Primitives.Extensions;

/// <summary>
/// Provides extension methods for inspecting the result of a completed process.
/// </summary>
public static class ProcessExtensions
{
    extension(Process process)
    {
        /// <summary>
        /// Determines whether the process exited because an administrator elevation request was denied.
        /// </summary>
        /// <returns><see langword="true"/> when the exit code reports a denied elevation request.</returns>
        /// <exception cref="InvalidOperationException">The process has not exited.</exception>
        /// <remarks>
        /// Reads <see cref="Process.ExitCode"/> and classifies it with
        /// <see cref="ProcessHelper.IsExitCodeElevationDenied(int)"/>, so the macOS caveat applies: a cancelled
        /// prompt shares its exit code with an ordinary failure and cannot be recognized here. Capture the standard
        /// error and use <see cref="ProcessOutput"/> to cover macOS.
        /// </remarks>
        public bool IsExitCodeElevationDenied()
        {
            ArgumentNullException.ThrowIfNull(process);

            return ProcessHelper.IsExitCodeElevationDenied(process.ExitCode);
        }
    }

    extension(ProcessOutput output)
    {
        /// <summary>
        /// Determines whether the captured process reported that an administrator elevation request was denied.
        /// </summary>
        /// <returns><see langword="true"/> when the result reports a denied elevation request.</returns>
        /// <remarks>
        /// Classifies with <see cref="ProcessHelper.IsExitCodeElevationDenied(int, string?)"/>, so the captured
        /// standard error also resolves the macOS prompt that an exit code alone cannot.
        /// </remarks>
        public bool IsExitCodeElevationDenied()
        {
            ArgumentNullException.ThrowIfNull(output);

            return ProcessHelper.IsExitCodeElevationDenied(output.ExitCode, output.StandardError);
        }
    }
}
