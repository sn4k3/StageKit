using System.Globalization;

namespace StageKit.Primitives.System;

/// <summary>
/// Provides cross-platform system helper methods.
/// </summary>
public static partial class HostSystem
{
    /// <summary>
    /// Gets the string comparison used for file-system paths on the current platform.
    /// </summary>
    public static StringComparison HostStringComparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

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

    /// <summary>
    /// Produces a beep tone through the host speaker.
    /// </summary>
    /// <param name="frequency">The tone frequency in hertz. Clamped to <c>37</c> - <c>20000</c>.</param>
    /// <param name="duration">The duration of the beep in milliseconds. Values below <c>40</c> are raised to <c>40</c>.</param>
    /// <param name="waitForCompletion">
    /// <see langword="true"/> to wait for the tone to end; otherwise, the tone plays in the background.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the tone completed, or started when not waiting for completion; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// A beep is best-effort and never throws: a host without a console, audio device, or tone utility simply
    /// returns <see langword="false"/>. Only Windows and Linux honor <paramref name="frequency"/>; macOS falls back
    /// to the system alert sound, which has a fixed tone.
    /// </remarks>
    public static bool Beep(int frequency = 800, int duration = 150, bool waitForCompletion = false)
    {
        (frequency, duration) = NormalizeBeepArguments(frequency, duration);

        if (!OperatingSystem.IsWindows())
        {
            return ProcessHelper.StartShell(CreateBeepShellCommand(frequency, duration),
                waitForCompletion: waitForCompletion) == 0;
        }

        if (waitForCompletion) return TryWindowsBeep(frequency, duration);

        // TryWindowsBeep never throws, so this task cannot fault and become an unobserved task exception.
        _ = Task.Run(() => TryWindowsBeep(frequency, duration));
        return true;
    }

    /// <summary>
    /// Asynchronously produces a beep tone through the host speaker, completing when the tone ends.
    /// </summary>
    /// <param name="frequency">The tone frequency in hertz. Clamped to <c>37</c> - <c>20000</c>.</param>
    /// <param name="duration">The duration of the beep in milliseconds. Values below <c>40</c> are raised to <c>40</c>.</param>
    /// <param name="cancellationToken">The token used to cancel the beep before it starts.</param>
    /// <returns>
    /// A task containing <see langword="true"/> if the tone completed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// A beep is best-effort and never throws: a host without a console, audio device, or tone utility simply
    /// returns <see langword="false"/>. Only Windows and Linux honor <paramref name="frequency"/>; macOS falls back
    /// to the system alert sound, which has a fixed tone. Cancellation is only observed before the tone starts,
    /// because neither the host tone utility nor <see cref="Console.Beep(int, int)"/> can be interrupted.
    /// </remarks>
    public static Task<bool> BeepAsync(
        int frequency = 800,
        int duration = 150,
        CancellationToken cancellationToken = default)
    {
        (frequency, duration) = NormalizeBeepArguments(frequency, duration);

        return OperatingSystem.IsWindows()
            ? Task.Run(() => TryWindowsBeep(frequency, duration), cancellationToken)
            : BeepShellAsync(CreateBeepShellCommand(frequency, duration), cancellationToken);
    }

    private static async Task<bool> BeepShellAsync(string command, CancellationToken cancellationToken = default)
    {
        var exitCode = await ProcessHelper
            .StartShellAsync(command, waitForCompletion: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return exitCode == 0;
    }

    private static bool TryWindowsBeep(int frequency, int duration)
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility - callers guard with OperatingSystem.IsWindows().
            Console.Beep(frequency, duration);
#pragma warning restore CA1416 // Validate platform compatibility
            return true;
        }
        catch
        {
            // A missing console, audio device, or restrictive host policy must not surface from a beep.
            return false;
        }
    }

    /// <summary>
    /// Clamps beep arguments into the range every supported host accepts.
    /// </summary>
    /// <param name="frequency">The requested tone frequency in hertz.</param>
    /// <param name="duration">The requested duration in milliseconds.</param>
    /// <returns>The clamped frequency and duration.</returns>
    /// <remarks>
    /// <see cref="Console.Beep(int, int)"/> rejects frequencies below <c>37</c> hertz and durations of zero or less,
    /// so the lower bounds are enforced rather than passed through. The upper bound is the top of human hearing.
    /// </remarks>
    internal static (int Frequency, int Duration) NormalizeBeepArguments(int frequency, int duration)
    {
        return (Math.Clamp(frequency, 37, 20_000), Math.Max(40, duration));
    }

    /// <summary>
    /// Creates the shell command that plays a tone on non-Windows hosts.
    /// </summary>
    /// <param name="frequency">The tone frequency in hertz.</param>
    /// <param name="duration">The duration of the tone in milliseconds.</param>
    /// <returns>The shell command.</returns>
    /// <remarks>
    /// Prefers ALSA's <c>speaker-test</c> (Linux), then <c>osascript</c> (macOS), and finally the terminal bell.
    /// </remarks>
    internal static string CreateBeepShellCommand(int frequency, int duration)
    {
        var seconds = Math.Round(duration / 1000.0, 3, MidpointRounding.AwayFromZero)
            .ToString(CultureInfo.InvariantCulture);

        return $"if command -v speaker-test > /dev/null 2>&1; then " +
               $"speaker-test -t sine -f {frequency.ToString(CultureInfo.InvariantCulture)} -l 1 > /dev/null 2>&1 & " +
               $"sleep {seconds} && kill $! > /dev/null 2>&1; " +
               "elif command -v osascript > /dev/null 2>&1; then osascript -e beep > /dev/null 2>&1; " +
               "else printf '\\a'; fi";
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
}