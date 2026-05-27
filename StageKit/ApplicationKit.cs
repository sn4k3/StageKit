using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StageKit;

/// <summary>
/// Stores process-wide StageKit configuration used by settings, crash reporting, and logging helpers.
/// </summary>
public static partial class ApplicationKit
{
    /// <summary>
    /// Gets or sets the logger used by StageKit helpers.
    /// </summary>
    public static ILogger? Logger { get; set; }

    /// <summary>
    /// Gets the timestamp when the application started, used as a reference point for calculating runtime durations.
    /// </summary>
    public static long StartingTimestamp { get; } = Stopwatch.GetTimestamp();

    /// <summary>
    /// Gets the elapsed time since the application started.
    /// </summary>
    /// <remarks>This property provides a high-resolution measurement of the application's runtime duration.
    /// The value is calculated from the moment the application process began.</remarks>
    public static TimeSpan RuntimeElapsed => Stopwatch.GetElapsedTime(StartingTimestamp);

    /// <summary>
    /// Gets or sets the command-line arguments for the current application instance.
    /// </summary>
    /// <remarks>
    /// Setting this property updates <see cref="HasCrashReportFlag"/> and
    /// <see cref="CrashReportIndex"/> when <see cref="CrashReportFlag"/> is present.
    /// </remarks>
    public static string[]? ApplicationArgs
    {
        get;
        set
        {
            field = value;
            HasCrashReportFlag = false;
            CrashReportIndex = 0;
            if (field is null || string.IsNullOrWhiteSpace(CrashReportFlag)) return;
            var crashReportIndex = Array.IndexOf(field, CrashReportFlag);
            if (crashReportIndex >= 0 && field.Length > crashReportIndex + 1)
            {
                _ = long.TryParse(field[crashReportIndex + 1], out var crashReportHashCode);
                if (crashReportHashCode > 0)
                {
                    CrashReportIndex = crashReportHashCode;
                }

                HasCrashReportFlag = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the application name used to build default data paths.
    /// </summary>
    public static string ApplicationName { get; set; } = AppDomain.CurrentDomain.FriendlyName;

    /// <summary>
    /// Gets or sets optional UI framework information included in crash reports.
    /// </summary>
    public static string? UiFrameworkInfo { get; set; }

    /// <summary>
    /// Gets or sets the command-line argument used to open a crash report instance.
    /// </summary>
    public static string? CrashReportFlag { get; set; } = "--crash-report";

    /// <summary>
    /// Gets a value indicating whether <see cref="ApplicationArgs"/> contains <see cref="CrashReportFlag"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CrashReport))]
    public static bool HasCrashReportFlag { get; private set; }

    /// <summary>
    /// Gets the crash report identifier parsed from <see cref="ApplicationArgs"/>.
    /// </summary>
    /// <remarks>0 indicates no crash report is active.</remarks>
    public static long CrashReportIndex { get; private set; }

    /// <summary>
    /// Gets the active crash report when <see cref="HasCrashReportFlag"/> is <see langword="true"/> and <see cref="CrashReportIndex"/> is greater than 0; otherwise, <see langword="null"/>.
    /// </summary>
    public static CrashReport? CrashReport => HasCrashReportFlag && CrashReportIndex > 0 ? CrashReportsFile.Instance.GetActual(CrashReportIndex) : null;

    /// <summary>
    /// Gets or sets the root profile directory for application data.
    /// </summary>
    public static string ProfilePath { get; set; } = GetDefaultProfilePath();

    /// <summary>
    /// Gets or sets the directory name used for log and crash report files within the profile path.
    /// </summary>
    public static string LogsDirectoryName { get; set; } = "logs";

    /// <summary>
    /// Gets the directory used for log and crash report files.
    /// </summary>
    public static string LogsPath => Path.Combine(ProfilePath, LogsDirectoryName);

    /// <summary>
    /// Gets or sets the directory name used for config files within the profile path.
    /// </summary>
    public static string ConfigsDirectoryName { get; set; } = "configs";

    /// <summary>
    /// Gets the directory used for config files.
    /// </summary>
    public static string ConfigsPath => Path.Combine(ProfilePath, ConfigsDirectoryName);

    /// <summary>
    /// Gets or sets the directory name used for backup files within the profile path.
    /// </summary>
    public static string BackupsDirectoryName { get; set; } = "backups";

    /// <summary>
    /// Gets the directory used for backup files.
    /// </summary>
    public static string BackupsPath => Path.Combine(ProfilePath, BackupsDirectoryName);

    /// <summary>
    /// Gets the default profile path for the current operating system.
    /// </summary>
    /// <returns>The default directory path for application data.</returns>
    public static string GetDefaultProfilePath()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ApplicationName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                ApplicationName);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ApplicationName);
    }

    /// <summary>
    /// Gets or sets the JSON serializer options used by StageKit settings and crash report files.
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions { get; set; } = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        }
    };
}
