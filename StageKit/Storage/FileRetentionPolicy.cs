namespace StageKit;

/// <summary>
/// Describes how files should be retained in a directory.
/// </summary>
public sealed class FileRetentionPolicy
{
    /// <summary>
    /// Gets or sets the maximum file age. Older files are deleted when set.
    /// </summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of files retained after age-based deletion. The newest files are kept.
    /// </summary>
    public int MaxFiles { get; set; }

    /// <summary>
    /// Gets or sets the file search pattern.
    /// </summary>
    public string SearchPattern { get; set; } = "*";

    /// <summary>
    /// Gets or sets a value indicating whether child directories are searched.
    /// </summary>
    public bool Recursive { get; set; }
}
