namespace StageKit;

/// <summary>
/// Configures support bundle export.
/// </summary>
public sealed class SupportBundleOptions
{
    /// <summary>
    /// Gets or sets the destination zip file path. Defaults to a timestamped file under <see cref="ApplicationKit.BackupsPath"/>.
    /// </summary>
    public string? DestinationFilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether config files are included.
    /// </summary>
    public bool IncludeConfigs { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether log files are included.
    /// </summary>
    public bool IncludeLogs { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the crash report file is included.
    /// </summary>
    public bool IncludeCrashReports { get; set; } = true;

    /// <summary>
    /// Gets additional files to include in the support bundle.
    /// </summary>
    public IList<string> AdditionalFilePaths { get; } = [];

    /// <summary>
    /// Gets or sets optional caller-supplied notes written into the support bundle manifest.
    /// </summary>
    public string? Notes { get; set; }
}
