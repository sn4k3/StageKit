namespace StageKit.Runtime;

/// <summary>
/// Defines how an application is packaged for publishing or distribution.
/// </summary>
[Flags]
public enum ApplicationPackagingType
{
    /// <summary>
    /// No application packaging type is selected or known.
    /// </summary>
    None = 0,

    /// <summary>
    /// The application is not bundled and runs under a raw folder.
    /// </summary>
    Portable = 1 << 0,

    /// <summary>
    /// The application is bundled as a .NET single-file application (PublishSingleFile).
    /// </summary>
    DotNetSingleFile = 1 << 1,

    /// <summary>
    /// The application is bundled as a Windows Installer (MSI) package.
    /// </summary>
    WindowsInstaller = 1 << 2,

    /// <summary>
    /// The application is bundled as a Linux AppImage.
    /// </summary>
    LinuxAppImage = 1 << 3,

    /// <summary>
    /// The application is bundled as a Linux Flatpak.
    /// </summary>
    LinuxFlatpak = 1 << 4,

    /// <summary>
    /// The application is bundled as a macOS app bundle.
    /// </summary>
    MacOSAppBundle = 1 << 5
}
