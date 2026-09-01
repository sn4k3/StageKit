namespace StageKit.Runtime;

/// <summary>
/// Defines how an application is packaged for publishing or distribution.
/// </summary>
public enum ApplicationPackagingType
{
    /// <summary>
    /// No application packaging type is selected or known.
    /// </summary>
    None,

    /////////////
    // Generic //
    /////////////

    /// <summary>
    /// The application is not bundled and runs under a raw folder.
    /// </summary>
    Portable,

    /// <summary>
    /// The application is bundled as a .NET single-file application (PublishSingleFile).
    /// </summary>
    DotNetSingleFile,

    /////////////
    // Windows //
    /////////////

    /// <summary>
    /// The application is bundled as a Windows Installer (MSI) package.
    /// </summary>
    WindowsInstaller,

    ///////////
    // Linux //
    ///////////

    /// <summary>
    /// The application is bundled as a Linux AppImage.
    /// </summary>
    LinuxAppImage,

    /// <summary>
    /// The application is bundled as a Linux Flatpak.
    /// </summary>
    LinuxFlatpak,

    /// <summary>
    /// The application is bundled as a Snap package.
    /// </summary>
    LinuxSnap,

    /// <summary>
    /// The application is bundled as a Debian package.
    /// </summary>
    LinuxDeb,

    /// <summary>
    /// The application is bundled as an RPM package.
    /// </summary>
    LinuxRpm,

    /// <summary>
    /// The application is bundled as an Arch Linux binary package (<c>.pkg.tar.zst</c>).
    /// </summary>
    LinuxArchPackage,

    ///////////
    // macOS //
    ///////////

    /// <summary>
    /// The application is bundled as a macOS app bundle.
    /// </summary>
    MacOSAppBundle,

    /// <summary>
    /// The application is bundled as a compressed macOS disk image.
    /// </summary>
    MacOSDmg,

    /// <summary>
    /// The application is bundled as a macOS Installer component package.
    /// </summary>
    MacOSPkg
}