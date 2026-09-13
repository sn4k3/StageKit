using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;
using Fallout.Solutions;
using Serilog;
using StageKit.Runtime;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Creates the publish settings used for an installer payload.
    /// </summary>
    /// <param name="context">The runtime publish context.</param>
    /// <param name="outputPath">The temporary publish output path.</param>
    /// <returns>Settings for the installer publish.</returns>
    protected virtual DotNetPublishSettings CreateWindowsInstallerPublishSettings(
        PublishRidContext context,
        AbsolutePath outputPath)
    {
        var useSingleFile = WindowsInstallerOptions.UseSingleFile;
        return useSingleFile
            ? CreateSingleFilePublishSettings(context, outputPath)
            : CreatePublishSettings(context)
                .SetOutput(outputPath)
                .SetPublishSingleFile(false)
                .SetProperty("DebugType", "portable")
                .SetProperty("PublishDocumentationFiles", true)
                .SetProperty("IncludeAllContentForSelfExtract", false)
                .SetProperty("IncludeNativeLibrariesForSelfExtract", false)
                .DisableNoRestore();
    }

    /// <summary>
    /// Publishes the payload used by an installer when custom staging is required.
    /// </summary>
    /// <param name="context">The runtime publish context.</param>
    /// <param name="outputPath">The temporary publish output path.</param>
    protected virtual void PublishWindowsInstallerPayload(PublishRidContext context, AbsolutePath outputPath)
    {
        outputPath.DeleteDirectory();
        outputPath.CreateDirectory();
        ExecuteDotNetPublish(CreateWindowsInstallerPublishSettings(context, outputPath));
    }

    /// <summary>
    /// Creates Windows installers from one published Windows runtime output.
    /// </summary>
    /// <param name="context">The runtime publish context.</param>
    /// <param name="platform">The installer platform.</param>
    protected virtual void CreateWindowsInstallers(PublishRidContext context, string platform)
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Warning("Skipping Windows installers for {RuntimeIdentifier} on a non-Windows host.",
                context.RuntimeIdentifier);
            return;
        }

        var installerProjects = InstallerProjects;
        if (installerProjects.Count == 0)
        {
            Log.Warning("Skipping Windows installers for {RuntimeIdentifier} because no installer project was found.",
                context.RuntimeIdentifier);
            return;
        }

        Log.Information("Creating {fileName} Windows installer bundle for {Rid} ({Platform})",
            string.Concat(context.BundleOutputPath.Name, ".msi"), context.RuntimeIdentifier, platform);

        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        AbsolutePath? normalPublishPath = null;
        try
        {
            var installerSourcePath = context.PublishPath;
            var useSingleFile = WindowsInstallerOptions.UseSingleFile;
            if (useSingleFile)
            {
                var installerPublishPath = InstallerPayloadDirectory / Guid.NewGuid().ToString("N");
                normalPublishPath = installerPublishPath;
                PublishWindowsInstallerPayload(context, installerPublishPath);
                installerSourcePath = installerPublishPath;
            }

            stagingPath.DeleteDirectory();
            installerSourcePath.Copy(stagingPath, ExistsPolicy.MergeAndOverwrite);
            PublishUtilities.WriteRuntimeManifest(stagingPath, BuildRuntimeManifestFileName,
                new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true,
                    ApplicationPackagingType.WindowsInstaller));

            foreach (var project in installerProjects)
                BuildWindowsInstaller(project, context, stagingPath, platform);
        }
        finally
        {
            stagingPath.DeleteDirectory();
            normalPublishPath?.DeleteDirectory();
        }
    }

    /// <summary>
    /// Builds one installer project from a staged published payload.
    /// </summary>
    /// <param name="project">The installer project.</param>
    /// <param name="context">The runtime publish context.</param>
    /// <param name="sourcePath">The staged installer payload.</param>
    /// <param name="platform">The installer platform.</param>
    protected virtual void BuildWindowsInstaller(Project project, PublishRidContext context,
        AbsolutePath sourcePath, string platform)
    {
        DotNetBuild(settings => ConfigureWindowsInstallerBuildSettings(
            settings, project, context, sourcePath, platform));
    }

    /// <summary>
    /// Configures the build settings used for one installer project.
    /// </summary>
    /// <param name="settings">The settings to configure.</param>
    /// <param name="project">The installer project.</param>
    /// <param name="context">The runtime publish context.</param>
    /// <param name="sourcePath">The staged installer payload.</param>
    /// <param name="platform">The installer platform.</param>
    /// <returns>The configured installer build settings.</returns>
    protected virtual DotNetBuildSettings ConfigureWindowsInstallerBuildSettings(
        DotNetBuildSettings settings,
        Project project,
        PublishRidContext context,
        AbsolutePath sourcePath,
        string platform)
    {
        var options = WindowsInstallerOptions;
        var certificateThumbprint = !string.IsNullOrWhiteSpace(WindowsAuthenticodeCertificateThumbprint)
            ? WindowsAuthenticodeCertificateThumbprint
            : options.AuthenticodeCertificateThumbprint;
        var iconFile = options.IconFile ?? WindowsIconFile;

        settings = settings
            .SetProjectFile(project)
            .SetConfiguration(Configuration)
            .SetPlatform(platform)
            .SetOutputDirectory(PublishDirectory)
            .SetRuntime(context.RuntimeIdentifier)
            .SetProperty("PublishDirectory", sourcePath)
            .SetProperty("BuildVersion", SoftwareVersion)
            .SetProperty("ApplicationName", SoftwareName)
            .SetProperty("ApplicationExecutableName", SoftwareExecutableFileNameWithoutExtension)
            .SetProperty("Company", PublishUtilities.EscapeMSBuildPropertyValue(SoftwareCompany))
            .SetProperty("Copyright", PublishUtilities.EscapeMSBuildPropertyValue(SoftwareCopyright))
            .SetProperty("Description", PublishUtilities.EscapeMSBuildPropertyValue(SoftwareDescription))
            .SetProperty("Keywords", PublishUtilities.EscapeMSBuildPropertyValue(SoftwareKeywords))
            .SetProperty("RepositoryUrl", SoftwareRepositoryUrl)
            .SetProperty("ApplicationIcon", iconFile)
            .SetProperty("AuthenticodeCertificateThumbprint", certificateThumbprint ?? string.Empty)
            .SetProperty("AuthenticodeTimestampUrl", options.AuthenticodeTimestampUrl)
            .SetProperty("SignToolPath", options.SignToolPath)
            .SetProperty("OutputName", context.BundleOutputPath.Name);

        if (options.InstallerScope is { } installerScope)
        {
            var scopeValue = installerScope switch
            {
                InstallerScope.PerMachineOrUser => "perMachineOrUser",
                InstallerScope.PerUser => "perUser",
                InstallerScope.PerMachine => "perMachine",
                _ => throw new ArgumentOutOfRangeException(nameof(options.InstallerScope), installerScope, "Unsupported installer scope.")
            };
            settings = settings.SetProperty("InstallerScope", scopeValue);
        }

        if (options.PathRegistration is { } pathRegistration and not PathRegistration.None)
        {
            var pathRegistrationValue = pathRegistration switch
            {
                PathRegistration.Register => "Register",
                PathRegistration.UserDefaultNo => "UserDefaultNo",
                PathRegistration.UserDefaultYes => "UserDefaultYes",
                _ => throw new ArgumentOutOfRangeException(nameof(options.PathRegistration), pathRegistration, "Unsupported PATH registration mode.")
            };
            settings = settings.SetProperty("InstallerPathRegistration", pathRegistrationValue);
        }

        var contextMenuFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var association in options.ContextMenuOpenWithFileAssociations)
        {
            foreach (var ext in association.Extensions)
            {
                var trimmed = ext.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    contextMenuFiles.Add(trimmed);
                }
            }
        }

        if (contextMenuFiles.Count > 0)
        {
            var files = string.Join(";", contextMenuFiles);
            settings = settings.SetProperty("ContextMenuOpenWithFileAssociations",
                PublishUtilities.EscapeMSBuildPropertyValue(files));
        }

        if (options.FileAssociations.Count > 0)
        {
            var extensions = options.FileAssociations
                .SelectMany(assoc => assoc.Extensions)
                .Select(ext => ext.Trim().TrimStart('*'))
                .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
                .Where(ext => ext.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase);

            var fileAssocString = string.Join(";", extensions);
            if (!string.IsNullOrWhiteSpace(fileAssocString))
            {
                settings = settings.SetProperty("FileAssociations",
                    PublishUtilities.EscapeMSBuildPropertyValue(fileAssocString));
            }
        }

        if (options.UrlSchemes.Count > 0)
        {
            var schemes = options.UrlSchemes
                .Select(s => s.Trim().TrimEnd(':', '/'))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

            var urlSchemesString = string.Join(";", schemes);
            if (!string.IsNullOrWhiteSpace(urlSchemesString))
            {
                settings = settings.SetProperty("UrlSchemes",
                    PublishUtilities.EscapeMSBuildPropertyValue(urlSchemesString));
            }
        }

        if (!string.IsNullOrWhiteSpace(options.LaunchApplicationArguments))
        {
            settings = settings.SetProperty("LaunchApplicationArguments",
                PublishUtilities.EscapeMSBuildPropertyValue(options.LaunchApplicationArguments));
        }

        if (!options.DefaultDesktopShortcut)
        {
            settings = settings.SetProperty("DefaultDesktopShortcut", "0");
        }

        if (!options.DefaultStartMenuShortcut)
        {
            settings = settings.SetProperty("DefaultStartMenuShortcut", "0");
        }

        if (!string.IsNullOrWhiteSpace(options.ContextMenuTitle))
        {
            settings = settings.SetProperty("ContextMenuOpenWithTitle",
                PublishUtilities.EscapeMSBuildPropertyValue(options.ContextMenuTitle));
        }

        if (!string.IsNullOrWhiteSpace(options.ContextMenuCommandArguments))
        {
            settings = settings.SetProperty("ContextMenuOpenWithCommandArgs",
                PublishUtilities.EscapeMSBuildPropertyValue(options.ContextMenuCommandArguments));
        }

        return settings;
    }

    /// <summary>
    /// Determines whether a project is an installer project.
    /// </summary>
    /// <param name="project">The project to inspect.</param>
    /// <returns><c>true</c> when the project is a WiX project; otherwise, <c>false</c>.</returns>
    protected virtual bool IsWindowsInstallerProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return PublishUtilities.IsWixProject(project.Path);
    }
}