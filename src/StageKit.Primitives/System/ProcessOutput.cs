namespace StageKit.Primitives.System;

/// <summary>
/// Represents the completed output of a process.
/// </summary>
public sealed record ProcessOutput
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessOutput"/> class.
    /// </summary>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="standardOutput">The text written to standard output.</param>
    /// <param name="standardError">The text written to standard error.</param>
    public ProcessOutput(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>
    /// Gets the process exit code, or <c>-1</c> when the process could not be started.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets the text written to standard output.
    /// </summary>
    public string StandardOutput { get; }

    /// <summary>
    /// Gets the text written to standard error.
    /// </summary>
    public string StandardError { get; }

    /// <summary>
    /// Gets a value indicating whether the process completed successfully.
    /// </summary>
    public bool Succeeded => ExitCode == 0;
}
