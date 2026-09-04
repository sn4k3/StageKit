using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using StageKit.Primitives.System;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Gets the generated Bash GitHub Releases installation script path.
    /// </summary>
    public virtual AbsolutePath InstallScriptFile =>
        RootDirectory / "scripts" / $"install-{LinuxPackage.GetPackageName(SoftwareName)}.sh";

    /// <summary>
    /// Gets the generated Windows PowerShell GitHub Releases installation script path.
    /// </summary>
    public virtual AbsolutePath WindowsInstallScriptFile =>
        RootDirectory / "scripts" / $"install-{LinuxPackage.GetPackageName(SoftwareName)}.ps1";

    /// <summary>
    /// Generates Bash and Windows PowerShell installation scripts for the package formats selected by
    /// <see cref="PackagingTypes"/>.
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
    /// Creates the contents of the Windows GitHub Releases installation script.
    /// </summary>
    /// <returns>The complete PowerShell script.</returns>
    protected virtual string CreateWindowsInstallScript()
    {
        return WindowsInstallScript.Create(
            SoftwareRepositoryUrl,
            SoftwareName,
            SoftwareExecutableFileNameWithoutExtension,
            PackagingTypes);
    }

    /// <summary>
    /// Writes the generated installation scripts to <see cref="InstallScriptFile"/> and
    /// <see cref="WindowsInstallScriptFile"/>.
    /// </summary>
    protected virtual void ExecuteGenerateInstallScript()
    {
        var generatedAny = false;
        if (InstallScript.SupportsAny(PackagingTypes))
        {
            InstallScriptFile.Parent.CreateDirectory();
            InstallScriptFile.WriteAllText(CreateInstallScript());
            if (!OperatingSystem.IsWindows())
                UnixSystem.SetUnix755Executable(InstallScriptFile);

            Log.Information("Generated Bash installation script at {Path}", InstallScriptFile);
            generatedAny = true;
        }

        if (WindowsInstallScript.SupportsAny(PackagingTypes))
        {
            WindowsInstallScriptFile.Parent.CreateDirectory();
            WindowsInstallScriptFile.WriteAllText(CreateWindowsInstallScript());
            Log.Information("Generated PowerShell installation script at {Path}", WindowsInstallScriptFile);
            generatedAny = true;
        }

        if (!generatedAny)
        {
            throw new InvalidOperationException(
                "None of the selected Fallout packaging types can be installed by a generated script.");
        }
    }
}
