using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StageKit.Primitives;
using StageKit.Runtime;

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
    /// Gets the approximate <see cref="Stopwatch"/> timestamp when the current process started.
    /// </summary>
    public static long StartingTimestamp { get; } = GetProcessStartingTimestamp();

    /// <summary>
    /// Gets the elapsed time since the application started.
    /// </summary>
    /// <remarks>This property provides a high-resolution measurement of the application's runtime duration.
    /// The value is calculated from the moment the application process began.</remarks>
    public static TimeSpan RuntimeElapsed => Stopwatch.GetElapsedTime(StartingTimestamp);

    private static long GetProcessStartingTimestamp()
    {
        var currentTimestamp = Stopwatch.GetTimestamp();

        try
        {
            using var process = Process.GetCurrentProcess();
            var elapsedSinceProcessStart = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            if (elapsedSinceProcessStart <= TimeSpan.Zero) return currentTimestamp;

            var elapsedTimestampTicks = (long)(elapsedSinceProcessStart.TotalSeconds * Stopwatch.Frequency);
            return currentTimestamp - elapsedTimestampTicks;
        }
        catch
        {
            return currentTimestamp;
        }
    }

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
            ParseCrashReportArgs();
        }
    } = Environment.GetCommandLineArgs();

    /// <summary>
    /// Gets a unique session identifier for the current application instance, can be used to correlate logs and crash reports across the application's runtime.
    /// </summary>
    public static Guid SessionId { get; } =
#if NET10_0_OR_GREATER
        Guid.CreateVersion7();
#else
        Guid.NewGuid();
#endif

    /// <summary>
    /// Gets or sets the application name used to build default data paths.
    /// </summary>
    public static string ApplicationName
    {
        get;
        set
        {
            var previousDefaultProfilePath = GetDefaultProfilePath();
            var usesDefaultProfilePath =
                string.Equals(ProfilePath, previousDefaultProfilePath, StringComparison.Ordinal);

            field = value;
            if (usesDefaultProfilePath) ProfilePath = GetDefaultProfilePath();
        }
    } = EntryApplication.AssemblyName
        ?? EntryApplication.ExecutableName
        ?? EntryApplication.ProcessName;

    /// <summary>
    /// Gets or sets optional UI framework information included in crash reports.
    /// </summary>
    public static string? UiFrameworkInfo { get; set; }

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

        var crashReportIndex = Array.IndexOf(ApplicationArgs, CrashReportFlag);
        if (crashReportIndex < 0) return;

        HasCrashReportFlag = true;
        if (ApplicationArgs.Length <= crashReportIndex + 1) return;

        _ = long.TryParse(ApplicationArgs[crashReportIndex + 1], out var crashReportHashCode);
        if (crashReportHashCode > 0) CrashReportIndex = crashReportHashCode;
    }

    /// <summary>
    /// Gets or sets a value that indicates whether the application is running in portable mode.
    /// </summary>
    /// <remarks>
    /// <see cref="ParseProfilePathFromArgs"/> sets this value to <see langword="true"/> only when
    /// --portable parsing succeeds. A custom profile path does not enable portable mode.
    /// </remarks>
    public static bool IsPortable { get; set; }

    /// <summary>
    /// Gets or sets the directory name used for the portable profile.
    /// </summary>
    /// <remarks>Make sure to set this variable before calling <see cref="ParseCrashReportArgs"/>.<br/>
    /// <see cref="ApplicationName"/>_ will be prepended when folder not at same level as <see cref="EntryApplication.ProcessPath"/>.</remarks>
    public static string PortableProfileDirectoryName { get; set; } = "configs";

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
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ApplicationName);

        if (OperatingSystem.IsMacOS())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                ApplicationName);

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
            new JsonStringEnumConverter()
        }
    };

    /// <summary>
    /// Parses command-line arguments that can update the active profile path.
    /// </summary>
    /// <param name="args">The arguments to parse. Defaults to <see cref="ApplicationArgs"/>.</param>
    /// <param name="profilePathArg">The argument name whose following value contains the requested profile path, use <see langword="null"/> to disable.</param>
    /// <param name="portableArg">The argument name that enables portable mode with an optional parent-directory level, use <see langword="null"/> to disable.</param>
    public static void ParseProfilePathFromArgs(
        string[]? args = null,
        string? profilePathArg = "--profile-path",
        string? portableArg = "--portable")
    {
        args ??= ApplicationArgs;
        if (args is null) return;

        if (!string.IsNullOrWhiteSpace(portableArg))
        {
            var portableIndex = args.LastIndexOf(portableArg);
            if (portableIndex >= 0 && TryGetPortableProfilePath(args, portableIndex, out var portableProfilePath))
            {
                ProfilePath = portableProfilePath;
                IsPortable = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(profilePathArg))
        {
            var profilePathIndex = args.LastIndexOf(profilePathArg);
            if (profilePathIndex >= 0 && args.Length > profilePathIndex + 1)
            {
                var profilePath = args[profilePathIndex + 1];

                if (!string.IsNullOrWhiteSpace(profilePath) &&
                    PathUtilities.IsWritableOrCreatableDirectory(profilePath, out var fullProfilePath))
                {
                    ProfilePath = fullProfilePath;
                }
            }
        }
    }

    private static bool TryGetPortableProfilePath(string[] args, int portableIndex, out string portableProfilePath)
    {
        portableProfilePath = string.Empty;
        var portableLevel = 0;
        if (args.Length > portableIndex + 1 && int.TryParse(args[portableIndex + 1], out var parsedPortableLevel))
        {
            if (parsedPortableLevel < 0) return false;
            portableLevel = parsedPortableLevel;
        }

        var portableRootPath = EntryApplication.BaseDirectory ?? EntryApplication.AppContextBaseDirectory;
        for (var parentIndex = 0; parentIndex < portableLevel; parentIndex++)
        {
            var parent = Directory.GetParent(portableRootPath);
            if (parent is null) return false;

            portableRootPath = parent.FullName;
        }

        var configurationPath = portableRootPath == EntryApplication.ProcessPath
            ? Path.Combine(portableRootPath, PortableProfileDirectoryName)
            : Path.Combine(portableRootPath, $"{ApplicationName}_{PortableProfileDirectoryName}");
        if (!PathUtilities.TryGetValidFullDirectoryPath(configurationPath, out var fullConfigurationPath)) return false;

        try
        {
            Directory.CreateDirectory(fullConfigurationPath);
            portableProfilePath = fullConfigurationPath;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                       or UnauthorizedAccessException)
        {
            return false;
        }
    }
}