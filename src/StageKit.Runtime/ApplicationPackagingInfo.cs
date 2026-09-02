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
public record ApplicationPackagingInfo(
    ApplicationPackagingType PackagingType,
    string PackageName,
    string[] Extensions,
    OSPlatform? SupportedPlatform)
{
    /// <summary>
    /// Gets a dictionary of known application packaging types and their corresponding information.
    /// </summary>
    [field: MaybeNull]
    [field: AllowNull]
    public static IReadOnlyDictionary<ApplicationPackagingType, ApplicationPackagingInfo> KnownPackagingTypes =>
        field ??=
            new Dictionary<ApplicationPackagingType, ApplicationPackagingInfo>
            {
                {
                    ApplicationPackagingType.None,
                    new ApplicationPackagingInfo(ApplicationPackagingType.None, "None", [], null)
                },
                {
                    ApplicationPackagingType.Portable,
                    new ApplicationPackagingInfo(ApplicationPackagingType.Portable, "Portable",
                        [".zip", ".tar", ".tar.gz", ".tar.bz2", ".tar.xz"], null)
                },
                {
                    ApplicationPackagingType.DotNetSingleFile,
                    new ApplicationPackagingInfo(ApplicationPackagingType.DotNetSingleFile, ".NET Single File",
                    [
                        ".exe", ".bin",
                        string.Empty // Last two for linux/macOS single-file executables without extensions
                    ], null)
                },
                {
                    ApplicationPackagingType.WindowsInstaller,
                    new ApplicationPackagingInfo(ApplicationPackagingType.WindowsInstaller, "Windows Installer (MSI)",
                        [".msi", ".exe"], OSPlatform.Windows)
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
                    ApplicationPackagingType.LinuxDeb,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxDeb, "Linux Debian Package", [".deb"],
                        OSPlatform.Linux)
                },
                {
                    ApplicationPackagingType.LinuxRpm,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxRpm, "Linux RPM Package", [".rpm"],
                        OSPlatform.Linux)
                },
                {
                    ApplicationPackagingType.LinuxArchPackage,
                    new ApplicationPackagingInfo(ApplicationPackagingType.LinuxArchPackage, "Linux Arch Package",
                        [".pkg.tar.zst"], OSPlatform.Linux)
                },
                {
                    ApplicationPackagingType.MacOSAppBundle,
                    new ApplicationPackagingInfo(ApplicationPackagingType.MacOSAppBundle, "macOS App Bundle", [".zip"],
                        OSPlatform.OSX)
                },
                {
                    ApplicationPackagingType.MacOSDmg,
                    new ApplicationPackagingInfo(ApplicationPackagingType.MacOSDmg, "macOS DMG", [".dmg"],
                        OSPlatform.OSX)
                },
                {
                    ApplicationPackagingType.MacOSPkg,
                    new ApplicationPackagingInfo(ApplicationPackagingType.MacOSPkg, "macOS PKG", [".pkg"],
                        OSPlatform.OSX)
                }
            };
}