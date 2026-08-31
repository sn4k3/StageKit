namespace StageKit.Runtime;

/// <summary>
/// Defines how an application is packaged for publishing or distribution.
/// </summary>
public enum ApplicationPackagingType
{
    /// <summary>
    /// No application packaging type is selected or known.
    /// </summary>
    None = 0,

    /////////////
    // Generic //
    /////////////

    /// <summary>
    /// The application is not bundled and runs under a raw folder.
    /// </summary>
    Portable = 1 << 0,

    /// <summary>
    /// The application is bundled as a .NET single-file application (PublishSingleFile).
    /// </summary>
    DotNetSingleFile = 1 << 1,

    /////////////
    // Windows //
    /////////////

    /// <summary>
    /// The application is bundled as a Windows Installer (MSI) package.
    /// </summary>
    WindowsInstaller = 1 << 2,

    ///////////
    // Linux //
    ///////////

    /// <summary>
    /// The application is bundled as a Linux AppImage.
    /// </summary>
    LinuxAppImage = 1 << 3,

    /// <summary>
    /// The application is bundled as a Linux Flatpak.
    /// </summary>
    LinuxFlatpak = 1 << 4,

    /// <summary>
    /// The application is bundled as a Debian package.
    /// </summary>
    LinuxDeb = 1 << 6,

    /// <summary>
    /// The application is bundled as an RPM package.
    /// </summary>
    LinuxRpm = 1 << 7,

    /// <summary>
    /// The application is bundled as an Arch Linux binary package (<c>.pkg.tar.zst</c>).
    /// </summary>
    LinuxArchPackage = 1 << 8,

    /// <summary>
    /// The application is bundled as a Snap package.
    /// </summary>
    LinuxSnap = 1 << 9,

    ///////////
    // macOS //
    ///////////

    /// <summary>
    /// The application is bundled as a macOS app bundle.
    /// </summary>
    MacOSAppBundle = 1 << 5,

    /// <summary>
    /// The application is bundled as a compressed macOS disk image.
    /// </summary>
    MacOSDmg = 1 << 10,

    /// <summary>
    /// The application is bundled as a macOS Installer component package.
    /// </summary>
    MacOSPkg = 1 << 11
}
