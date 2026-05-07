namespace StageKit;

/// <summary>
/// Applies retention policies to application logs and crash reports.
/// </summary>
public static class ApplicationRetention
{
    /// <summary>
    /// Gets or sets the default log file retention policy.
    /// </summary>
    public static FileRetentionPolicy LogRetentionPolicy { get; set; } = new()
    {
        MaxAge = TimeSpan.FromDays(30),
        MaxFiles = 100,
        SearchPattern = "*",
        Recursive = true,
    };

    /// <summary>
    /// Applies <see cref="LogRetentionPolicy"/> to <see cref="ApplicationKit.LogsPath"/>.
    /// </summary>
    /// <returns>The retention result.</returns>
    public static FileRetentionResult ApplyLogRetention()
    {
        return ApplyFileRetention(ApplicationKit.LogsPath, LogRetentionPolicy);
    }

    /// <summary>
    /// Applies a file retention policy to a directory.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="policy">The retention policy.</param>
    /// <returns>The retention result.</returns>
    public static FileRetentionResult ApplyFileRetention(string directoryPath, FileRetentionPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(policy);

        var result = new FileRetentionResult();
        if (!Directory.Exists(directoryPath))
        {
            return result;
        }

        var searchOption = policy.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory
            .EnumerateFiles(directoryPath, policy.SearchPattern, searchOption)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        if (policy.MaxAge is { } maxAge)
        {
            var deleteBeforeUtc = DateTime.UtcNow - maxAge;
            foreach (var file in files.Where(file => file.LastWriteTimeUtc < deleteBeforeUtc).ToArray())
            {
                TryDelete(file.FullName, result);
                files.Remove(file);
            }
        }

        if (policy.MaxFiles > 0)
        {
            foreach (var file in files.Skip(policy.MaxFiles))
            {
                TryDelete(file.FullName, result);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies count and age retention to the crash report file.
    /// </summary>
    /// <param name="maxCrashReports">The maximum number of crash reports to retain. Uses <see cref="CrashReportsFile.MaxCrashReports"/> when omitted or less than 1.</param>
    /// <param name="maxAge">The maximum crash report age.</param>
    /// <returns>The number of crash reports removed.</returns>
    public static int ApplyCrashReportRetention(int maxCrashReports = 0, TimeSpan? maxAge = null)
    {
        return CrashReportsFile.Instance.ApplyRetention(maxCrashReports, maxAge);
    }

    private static void TryDelete(string filePath, FileRetentionResult result)
    {
        try
        {
            File.Delete(filePath);
            result.AddDeleted(filePath);
        }
        catch
        {
            result.AddFailed(filePath);
        }
    }
}
