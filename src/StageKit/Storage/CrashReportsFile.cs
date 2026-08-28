using System.Diagnostics.CodeAnalysis;

namespace StageKit;

/// <summary>
/// Persists recent crash reports in the application's settings storage.
/// </summary>
public class CrashReportsFile : RootCollectionFile<CrashReportsFile, CrashReport>
{
    /// <inheritdoc />
    public CrashReportsFile()
    {
        AutoSave = true;
        DirectoryPath = CrashReportsDirectoryPath;
        if (!string.IsNullOrWhiteSpace(CrashReportsFileName)) FileName = CrashReportsFileName;
        DefaultDebounceSaveMilliseconds = 0; // Need instant save when capturing crash reports
        TrimCollectionWhenExceeding = MaxCrashReports;
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

    /// <summary>
    /// Applies count and age retention to the crash reports in this file.
    /// </summary>
    /// <param name="maxCrashReports">The maximum number of reports to retain. Uses <see cref="MaxCrashReports"/> when omitted or less than 1.</param>
    /// <param name="maxAge">The maximum report age.</param>
    /// <returns>The number of reports removed.</returns>
    public int ApplyRetention(int maxCrashReports = 0, TimeSpan? maxAge = null)
    {
        if (maxCrashReports <= 0)
        {
            maxCrashReports = MaxCrashReports;
        }

        var removed = 0;
        using (SuspendAutoSave(false))
        {
            if (maxAge is { } age)
            {
                var deleteBeforeUtc = DateTime.UtcNow - age;
                foreach (var report in this.Where(report => report.DateTimeUtc < deleteBeforeUtc).ToArray())
                {
                    Remove(report);
                    removed++;
                }
            }

            if (maxCrashReports > 0 && Count > maxCrashReports)
            {
                foreach (var report in this
                             .OrderByDescending(report => report.DateTimeUtc)
                             .Skip(maxCrashReports)
                             .ToArray())
                {
                    Remove(report);
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            Save();
        }

        return removed;
    }

    #region Static Configuration

    /// <summary>
    /// Gets or sets a value indicating whether the automatic crash report capture and persistence features are enabled.
    /// </summary>
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the directory used for crash report storage. When not explicitly configured, this follows
    /// <see cref="ApplicationKit.LogsPath"/>.
    /// </summary>
    [field: MaybeNull]
    [field: AllowNull]
    public static string CrashReportsDirectoryPath
    {
        get => field ?? ApplicationKit.LogsPath;
        set;
    }

    /// <summary>
    /// Gets or sets the crash report JSON file name.
    /// </summary>
    public static string CrashReportsFileName { get; set; } = "CrashReports.json";

    /// <summary>
    /// Gets or sets the maximum number of crash reports retained in the file.
    /// </summary>
    public static int MaxCrashReports { get; set; } = 50;

    #endregion
}