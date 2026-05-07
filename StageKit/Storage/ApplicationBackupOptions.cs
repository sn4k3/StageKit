namespace StageKit;

/// <summary>
/// Configures application profile backup creation.
/// </summary>
public sealed class ApplicationBackupOptions
{
    /// <summary>
    /// Gets or sets the source directory to back up. Defaults to <see cref="ApplicationKit.ProfilePath"/>.
    /// </summary>
    public string SourceDirectoryPath { get; set; } = ApplicationKit.ProfilePath;

    /// <summary>
    /// Gets or sets the destination zip file path. Defaults to a timestamped file under <see cref="ApplicationKit.BackupsPath"/>.
    /// </summary>
    public string? DestinationFilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether files under <see cref="ApplicationKit.BackupsPath"/> are included.
    /// </summary>
    public bool IncludeBackupsDirectory { get; set; }
}
