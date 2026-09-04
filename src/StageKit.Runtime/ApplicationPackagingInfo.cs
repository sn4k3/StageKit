using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace StageKit.Runtime;

/// <summary>
/// Represents information about an application packaging type, including its name and associated file extensions.
/// </summary>
/// <param name="PackagingType">The type of application packaging.</param>
/// <param name="PackageName">The name of the package.</param>
/// <param name="Extensions">The file extensions associated with the packaging type.</param>
/// <param name="SupportedPlatform">The platform supported by the packaging type.</param>
/// <param name="DistroSpecific">Indicates whether the packaging type is specific to a particular Linux distribution.</param>
public record ApplicationPackagingInfo(
    ApplicationPackagingType PackagingType,
    string PackageName,
    string[] Extensions,
    OSPlatform? SupportedPlatform,
    bool DistroSpecific = false)
{
    /// <summary>
    /// Gets a value indicating whether the packaging type is supported on the current platform.
    /// </summary>
    public bool IsSupportedOnCurrentPlatform =>
        SupportedPlatform is null || RuntimeInformation.IsOSPlatform(SupportedPlatform.Value);

    /// <summary>
    /// Gets a dictionary of known application packaging types and their corresponding information.
    /// </summary>
    /// <remarks>
    /// Enumeration order is the preferred package order for consumers that must choose between multiple formats:
    /// platform-native installers precede generic bundles, and portable packages are last.
    /// </remarks>
    [field: MaybeNull]
    [field: AllowNull]
    public static IReadOnlyDictionary<ApplicationPackagingType, ApplicationPackagingInfo> KnownPackagingTypes =>
        field ??=
            new Dictionary<ApplicationPackagingType, ApplicationPackagingInfo>
            {
                {
                    ApplicationPackagingType.None,
                    new ApplicationPackagingInfo(ApplicationPackagingType.None, "None", [], OSPlatform.Create("None"))
                },
                {
                    ApplicationPackagingType.WindowsInstaller,
                    new ApplicationPackagingInfo(ApplicationPackagingType.WindowsInstaller, "Windows Installer (MSI)",
                        [".msi", ".exe"], OSPlatform.Windows)
                },
                {
                    ApplicationPackagingType.LinuxDeb,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxDeb, "Linux Debian Package", [".deb"],
                        OSPlatform.Linux, true)
                },
                {
                    ApplicationPackagingType.LinuxRpm,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxRpm, "Linux RPM Package", [".rpm"],
                        OSPlatform.Linux, true)
                },
                {
                    ApplicationPackagingType.LinuxArchPackage,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxArchPackage, "Linux Arch Package",
                        [".pkg.tar.zst"], OSPlatform.Linux, true)
                },
                {
                    ApplicationPackagingType.LinuxAppImage,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxAppImage, "Linux AppImage",
                        [".AppImage"], OSPlatform.Linux)
                },
                {
                    ApplicationPackagingType.LinuxFlatpak,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxFlatpak, "Linux Flatpak", [".flatpak"],
                        OSPlatform.Linux)
                },
                {
                    ApplicationPackagingType.LinuxSnap,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxSnap, "Linux Snap", [".snap"],
                        OSPlatform.Linux)
                },
                {
                    ApplicationPackagingType.MacOSAppBundle,
                    new ApplicationPackagingInfo(ApplicationPackagingType.MacOSAppBundle, "macOS App Bundle", [".zip"],
                        OSPlatform.OSX)
                },
                {
                    ApplicationPackagingType.MacOSPkg,
                    new ApplicationPackagingInfo(ApplicationPackagingType.MacOSPkg, "macOS PKG", [".pkg"],
                        OSPlatform.OSX)
                },
                {
                    ApplicationPackagingType.MacOSDmg,
                    new ApplicationPackagingInfo(ApplicationPackagingType.MacOSDmg, "macOS DMG", [".dmg"],
                        OSPlatform.OSX)
                },
                {
                    ApplicationPackagingType.DotNetSingleFile,
                    new ApplicationPackagingInfo(ApplicationPackagingType.DotNetSingleFile, ".NET Single File",
                    [
                        ".exe", ".bin",
                        string.Empty // Linux/macOS single-file executables may not have an extension
                    ], null)
                },
                {
                    ApplicationPackagingType.Portable,
                    new ApplicationPackagingInfo(ApplicationPackagingType.Portable, "Portable",
                        [".zip", ".tar", ".tar.gz", ".tar.bz2", ".tar.xz"], null)
                },
            };
}
