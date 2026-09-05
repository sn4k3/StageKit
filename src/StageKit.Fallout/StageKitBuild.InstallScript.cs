using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using StageKit.Primitives.System;
using StageKit.Runtime;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Gets the generated Bash GitHub Releases installation script path.
    /// </summary>
    public virtual AbsolutePath InstallScriptFile =>
        RootDirectory / "scripts" / $"install-{LinuxPackage.GetPackageName(SoftwareName)}.sh";

    /// <summary>
    /// Gets the generated Bash uninstallation script path.
    /// </summary>
    public virtual AbsolutePath UninstallScriptFile =>
        RootDirectory / "scripts" / $"uninstall-{LinuxPackage.GetPackageName(SoftwareName)}.sh";

    /// <summary>
    /// Gets the generated Windows PowerShell GitHub Releases installation script path.
    /// </summary>
    public virtual AbsolutePath WindowsInstallScriptFile =>
        RootDirectory / "scripts" / $"install-{LinuxPackage.GetPackageName(SoftwareName)}.ps1";

    /// <summary>
    /// Gets the generated Windows PowerShell uninstallation script path.
    /// </summary>
    public virtual AbsolutePath WindowsUninstallScriptFile =>
        RootDirectory / "scripts" / $"uninstall-{LinuxPackage.GetPackageName(SoftwareName)}.ps1";

    /// <summary>
    /// Gets or sets the exact WinGet package identifier that the generated Windows installation script tries before
    /// falling back to GitHub release assets. A null or whitespace value disables WinGet installation.
    /// </summary>
    public virtual string? WindowsInstallScriptWinGetPackageId { get; set; }

    /// <summary>
    /// Generates Bash and Windows PowerShell installation and uninstallation scripts for the package formats selected
    /// by <see cref="PackagingTypes"/>.
    /// </summary>
    /// <remarks>
    /// Package priority follows <see cref="StageKit.Runtime.ApplicationPackagingInfo.KnownPackagingTypes"/> order.
    /// Each generated script selects the first compatible package present in the requested GitHub release.
    /// </remarks>
    public virtual Target GenerateInstallScript => d => d
        .Executes(ExecuteGenerateInstallScript);

    /// <summary>
    /// Creates the contents of the Bash GitHub Releases installation script.
    /// </summary>
    /// <returns>The complete Bash script.</returns>
    protected virtual string CreateInstallScript()
    {
        return InstallScript.Create(
            SoftwareRepositoryUrl,
            SoftwareName,
            SoftwareExecutableFileNameWithoutExtension,
            PackagingTypes);
    }

    /// <summary>
    /// Creates the contents of the Bash uninstallation script.
    /// </summary>
    /// <returns>The complete Bash script.</returns>
    protected virtual string CreateUninstallScript()
    {
        var linuxApplicationId = PackagingTypes.Contains(ApplicationPackagingType.LinuxFlatpak)
            ? LinuxAppBundleOptions.ApplicationId
            : SoftwareRDNS;
        var macOSBundleIdentifier = PackagingTypes.Contains(ApplicationPackagingType.MacOSPkg)
            ? MacAppBundleOptions.BundleIdentifier
            : SoftwareRDNS;
        return UninstallScript.Create(
            SoftwareName,
            SoftwareExecutableFileNameWithoutExtension,
            linuxApplicationId,
            macOSBundleIdentifier);
    }

    /// <summary>
    /// Creates the contents of the Windows GitHub Releases installation script.
    /// </summary>
    /// <returns>The complete PowerShell script.</returns>
    protected virtual string CreateWindowsInstallScript()
    {
        return WindowsInstallScript.Create(
            SoftwareRepositoryUrl,
            SoftwareName,
            SoftwareExecutableFileNameWithoutExtension,
            PackagingTypes,
            WindowsInstallScriptWinGetPackageId);
    }

    /// <summary>
    /// Creates the contents of the Windows PowerShell uninstallation script.
    /// </summary>
    /// <returns>The complete PowerShell script.</returns>
    protected virtual string CreateWindowsUninstallScript()
    {
        return WindowsUninstallScript.Create(
            SoftwareName,
            WindowsInstallScriptWinGetPackageId ?? string.Empty);
    }

    /// <summary>
    /// Writes the generated scripts to <see cref="InstallScriptFile"/>, <see cref="UninstallScriptFile"/>,
    /// <see cref="WindowsInstallScriptFile"/>, and <see cref="WindowsUninstallScriptFile"/>.
    /// </summary>
    protected virtual void ExecuteGenerateInstallScript()
    {
        var generatedAny = false;
        if (InstallScript.SupportsAny(PackagingTypes))
        {
            InstallScriptFile.Parent.CreateDirectory();
            InstallScriptFile.WriteAllText(CreateInstallScript());
            UninstallScriptFile.Parent.CreateDirectory();
            UninstallScriptFile.WriteAllText(CreateUninstallScript());
            if (!OperatingSystem.IsWindows())
            {
                UnixSystem.SetUnix755Executable(InstallScriptFile);
                UnixSystem.SetUnix755Executable(UninstallScriptFile);
            }

            Log.Information("Generated Bash installation script at {Path}", InstallScriptFile);
            Log.Information("Generated Bash uninstallation script at {Path}", UninstallScriptFile);
            generatedAny = true;
        }

        if (WindowsInstallScript.SupportsAny(PackagingTypes))
        {
            WindowsInstallScriptFile.Parent.CreateDirectory();
            WindowsInstallScriptFile.WriteAllText(CreateWindowsInstallScript());
            WindowsUninstallScriptFile.Parent.CreateDirectory();
            WindowsUninstallScriptFile.WriteAllText(CreateWindowsUninstallScript());
            Log.Information("Generated PowerShell installation script at {Path}", WindowsInstallScriptFile);
            Log.Information("Generated PowerShell uninstallation script at {Path}", WindowsUninstallScriptFile);
            generatedAny = true;
        }

        if (!generatedAny)
        {
            throw new InvalidOperationException(
                "None of the selected Fallout packaging types can be installed by a generated script.");
        }
    }
}
