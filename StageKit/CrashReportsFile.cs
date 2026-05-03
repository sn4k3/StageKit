namespace StageKit;

/// <summary>
/// Persists recent crash reports in the application's settings storage.
/// </summary>
public class CrashReportsFile : RootCollectionFile<CrashReportsFile, CrashReport>
{
    #region Static Configuration
    /// <summary>
    /// Gets or sets a value indicating whether the automatic crash report capture and persistence features are enabled.
    /// </summary>
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the crash report JSON file name.
    /// </summary>
    public static string CrashReportFileName { get; set; } = "CrashReports.json";

    /// <summary>
    /// Gets or sets the maximum number of crash reports retained in the file.
    /// </summary>
    public static int MaxCrashReports { get; set; } = 50;
    #endregion

    /// <inheritdoc />
    public override string FileName => CrashReportFileName;

    /// <inheritdoc />
    protected override int DefaultDebounceSaveMilliseconds => 0;

    /// <inheritdoc />
    public override int TrimCollectionWhenExceeding => MaxCrashReports;

    /// <inheritdoc />
    public CrashReportsFile()
    {
        AutoSave = true;
    }

    /// <summary>
    /// Gets the crash report with the specified identifier.
    /// </summary>
    /// <param name="id">The crash report identifier.</param>
    /// <returns>The matching crash report, or <see langword="null"/> when no report exists.</returns>
    public CrashReport? GetActual(long id)
    {
        return id == 0
            ? null
            : this.FirstOrDefault(report => report.Id == id);
    }
}