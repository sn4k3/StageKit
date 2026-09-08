using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StageKit.Runtime;

namespace StageKit;

/// <summary>
/// Stores process-wide StageKit configuration used by settings, crash reporting, and logging helpers.
/// </summary>
public static partial class ApplicationKit
{
    #region Properties

    /// <summary>
    /// Gets or sets the logger used by StageKit helpers.
    /// </summary>
    public static ILogger? Logger { get; set; }

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
    /// Gets or sets the JSON serializer options used by StageKit settings and log files.
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

    #endregion
}