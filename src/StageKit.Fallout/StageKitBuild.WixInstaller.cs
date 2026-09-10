using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using StageKit.Primitives;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Gets the name of the WiX installer project created by <see cref="GenerateWindowsWixInstaller"/>.
    /// </summary>
    public virtual string WindowsWixInstallerProjectName => $"{SoftwareName}.WixInstaller";

    /// <summary>
    /// Gets the directory that holds the generated WiX installer project.
    /// </summary>
    public virtual AbsolutePath WindowsWixInstallerDirectory =>
        RootDirectory / "builds" / WindowsWixInstallerProjectName;

    /// <summary>
    /// Gets the generated WiX installer project file.
    /// </summary>
    public virtual AbsolutePath WindowsWixInstallerProjectFile =>
        WindowsWixInstallerDirectory / $"{WindowsWixInstallerProjectName}.wixproj";

    /// <summary>
    /// Scaffolds a WiX installer project that the publish pipeline can build into an MSI.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Creates <see cref="WindowsWixInstallerDirectory"/> with the project file, <c>Package.wxs</c>,
    /// <c>Strings.en-us.wxl</c>, a readme, a placeholder <c>License.rtf</c>, and placeholder banner and dialog
    /// artwork. Every product-specific value is supplied by the pipeline at build time, so the generated project
    /// needs no editing to produce a working installer.
    /// </p>
    /// <p>
    /// The target never overwrites an existing project: it fails when the destination already exists so that
    /// customized authoring, and above all the upgrade codes, survive. Delete the directory to regenerate it.
    /// </p>
    /// </remarks>
    public virtual Target GenerateWindowsWixInstaller => d => d
        .Executes(ExecuteGenerateWindowsWixInstaller);

    /// <summary>
    /// Creates the placeholder license written to the generated project.
    /// </summary>
    /// <returns>A Rich Text Format document shown by the wizard's license agreement page.</returns>
    /// <remarks>Override to scaffold the real license instead of a placeholder.</remarks>
    protected virtual string CreateWindowsWixInstallerLicense()
    {
        return WixInstallerProject.CreateLicenseFile(SoftwareName, SoftwareCompany, SoftwareCopyright);
    }

    /// <summary>
    /// Creates a stable upgrade code for one installer platform.
    /// </summary>
    /// <returns>The generated upgrade code.</returns>
    /// <remarks>
    /// Called once per platform while scaffolding. The value is written into the project file and must never change
    /// afterwards, because Windows identifies a product family by its upgrade code.
    /// </remarks>
    protected virtual Guid CreateWindowsWixInstallerUpgradeCode()
    {
        return Guid.NewGuid();
    }

    /// <summary>
    /// Writes the generated WiX installer project.
    /// </summary>
    /// <exception cref="InvalidOperationException">The destination project already exists.</exception>
    protected virtual void ExecuteGenerateWindowsWixInstaller()
    {
        var projectName = FileUtilities.ValidatePathLeafName(
            WindowsWixInstallerProjectName,
            nameof(WindowsWixInstallerProjectName));
        var directory = WindowsWixInstallerDirectory;
        var projectFile = WindowsWixInstallerProjectFile;

        WarnExistingWindowsInstallerProjects();
        ThrowIfWindowsWixInstallerExists(directory, projectFile);

        var resourcesDirectory = directory / "Resources";
        resourcesDirectory.CreateDirectory();

        projectFile.WriteAllText(WixInstallerProject.CreateProjectFile(
            CreateWindowsWixInstallerUpgradeCode(),
            CreateWindowsWixInstallerUpgradeCode()));
        (directory / "Package.wxs").WriteAllText(WixInstallerProject.CreatePackageFile());
        (directory / "Strings.en-us.wxl").WriteAllText(WixInstallerProject.CreateLocalizationFile());
        (directory / "README.md").WriteAllText(WixInstallerProject.CreateReadmeFile(projectName));
        (resourcesDirectory / "License.rtf").WriteAllText(CreateWindowsWixInstallerLicense());
        File.WriteAllBytes(resourcesDirectory / "InstallerBannerImage.png", WixInstallerProject.CreateBannerImage());
        File.WriteAllBytes(resourcesDirectory / "InstallerDialogImage.png", WixInstallerProject.CreateDialogImage());

        Log.Information("Generated the WiX installer project at {Path}", projectFile);
        Log.Information(
            "Add {ProjectFile} to the solution so the publish pipeline discovers it, then replace the placeholder " +
            "license and artwork in {ResourcesDirectory}",
            projectFile,
            resourcesDirectory);
    }

    /// <summary>
    /// Logs the WiX installer projects the solution already contains.
    /// </summary>
    protected virtual void WarnExistingWindowsInstallerProjects()
    {
        var existingProjects = InstallerProjects;
        if (existingProjects.Count == 0) return;

        Log.Warning(
            "The solution already contains {Count} WiX installer project(s), which the publish pipeline builds " +
            "in addition to the generated one: {Projects}",
            existingProjects.Count,
            string.Join(", ", existingProjects.Select(project => project.Name)));
    }

    /// <summary>
    /// Fails when a WiX installer project already occupies the generated destination.
    /// </summary>
    /// <param name="directory">The destination directory.</param>
    /// <param name="projectFile">The destination project file.</param>
    /// <exception cref="InvalidOperationException">The destination already exists.</exception>
    protected virtual void ThrowIfWindowsWixInstallerExists(AbsolutePath directory, AbsolutePath projectFile)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(projectFile);

        string reason;
        if (projectFile.FileExists())
            reason = $"the project file '{projectFile}' already exists";
        else if (directory.DirectoryExists() && Directory.EnumerateFileSystemEntries(directory).Any())
            reason = $"the directory '{directory}' already exists and is not empty";
        else
            return;

        Log.Warning(
            "Skipping WiX installer generation because {Reason}. Delete it first to regenerate the project, and " +
            "keep a copy of its upgrade codes so installed versions keep upgrading instead of installing twice.",
            reason);
        throw new InvalidOperationException(
            $"Cannot generate the WiX installer project '{WindowsWixInstallerProjectName}' because {reason}.");
    }
}
