namespace StageKit.Fallout;

/// <summary>
/// Specifies the PATH environment variable registration mode for a Windows installer package.
/// </summary>
public enum PathRegistration
{
    /// <summary>
    /// PATH registration is omitted from the installer package.
    /// </summary>
    None = 0,

    /// <summary>
    /// Always adds the installation directory to the PATH environment variable without presenting a checkbox in the installer wizard.
    /// </summary>
    Register,

    /// <summary>
    /// Displays a checkbox in the installer wizard to add the installation directory to PATH, unchecked by default.
    /// </summary>
    UserDefaultNo,

    /// <summary>
    /// Displays a checkbox in the installer wizard to add the installation directory to PATH, checked by default.
    /// </summary>
    UserDefaultYes
}
