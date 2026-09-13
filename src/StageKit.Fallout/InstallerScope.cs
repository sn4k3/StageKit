namespace StageKit.Fallout;

/// <summary>
/// Specifies the installation scope for a Windows installer package.
/// </summary>
public enum InstallerScope
{
    /// <summary>
    /// The installer allows the user to choose between installing for the current user or for all users.
    /// </summary>
    PerMachineOrUser,

    /// <summary>
    /// The installer installs only for the current user (per-user).
    /// </summary>
    PerUser,

    /// <summary>
    /// The installer installs for all users on the machine (per-machine), requiring administrative elevation.
    /// </summary>
    PerMachine
}
