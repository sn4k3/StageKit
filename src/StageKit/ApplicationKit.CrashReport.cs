namespace StageKit;

public static partial class ApplicationKit
{
    /// <summary>
    /// Gets or sets the command-line argument used to open a crash report instance.
    /// </summary>
    public static string? CrashReportFlag
    {
        get;
        set
        {
            field = value;
            ParseCrashReportArgs();
        }
    } = "--crash-report";

    /// <summary>
    /// Gets a value indicating whether <see cref="ApplicationArgs"/> contains <see cref="CrashReportFlag"/>.
    /// </summary>
    public static bool HasCrashReportFlag { get; private set; }

    /// <summary>
    /// Gets the crash report identifier parsed from <see cref="ApplicationArgs"/>.
    /// </summary>
    /// <remarks>0 indicates no crash report is active.</remarks>
    public static long CrashReportIndex { get; private set; }

    /// <summary>
    /// Gets the active crash report when <see cref="HasCrashReportFlag"/> is <see langword="true"/> and <see cref="CrashReportIndex"/> is greater than 0; otherwise, <see langword="null"/>.
    /// </summary>
    public static CrashReport? CrashReport => HasCrashReportFlag && CrashReportIndex > 0
        ? CrashReportsFile.Instance.GetActual(CrashReportIndex)
        : null;

    private static void ParseCrashReportArgs()
    {
        HasCrashReportFlag = false;
        CrashReportIndex = 0;
        if (ApplicationArgs is null || string.IsNullOrWhiteSpace(CrashReportFlag)) return;

        var crashReportIndex = Array.FindIndex(
            ApplicationArgs,
            argument => string.Equals(
                argument,
                CrashReportFlag,
                StringComparison.OrdinalIgnoreCase));

        if (crashReportIndex < 0) return;

        HasCrashReportFlag = true;
        if (ApplicationArgs.Length <= crashReportIndex + 1) return;

        _ = long.TryParse(ApplicationArgs[crashReportIndex + 1], out var crashReportHashCode);
        if (crashReportHashCode > 0) CrashReportIndex = crashReportHashCode;
    }
}