namespace StageKit;

public static partial class ApplicationKit
{
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
}