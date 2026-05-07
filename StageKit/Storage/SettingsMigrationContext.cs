namespace StageKit;

/// <summary>
/// Provides information to a settings migration hook.
/// </summary>
public readonly record struct SettingsMigrationContext
{
    /// <summary>
    /// Gets the settings version read from the loaded file.
    /// </summary>
    public int FromVersion { get; }

    /// <summary>
    /// Gets the settings version expected by the current application.
    /// </summary>
    public int ToVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsMigrationContext"/> class.
    /// </summary>
    /// <param name="fromVersion">The settings version read from the loaded file.</param>
    /// <param name="toVersion">The settings version expected by the current application.</param>
    public SettingsMigrationContext(int fromVersion, int toVersion)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }
}
