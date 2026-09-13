using System.Xml.Linq;
using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;
using Serilog;
using StageKit.Primitives.System;
using StageKit.Runtime;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Creates publish settings for a single-file application bundle.
    /// </summary>
    /// <param name="context">The runtime publish context.</param>
    /// <param name="outputPath">The temporary single-file publish output path.</param>
    /// <returns>The configured single-file publish settings.</returns>
    protected virtual DotNetPublishSettings CreateSingleFilePublishSettings(
        PublishRidContext context,
        AbsolutePath outputPath)
    {
        return new DotNetPublishSettings()
            .SetProject(MainProject.Path)
            .SetConfiguration(Configuration)
            .SetRuntime(context.RuntimeIdentifier)
            .SetOutput(outputPath)
            .SetSelfContained(!FrameworkDependent)
            .SetPublishReadyToRun(PublishReadyToRun)
            .SetPublishTrimmed(PublishTrimmed)
            .SetProperty("PublishAot", PublishAot)
            .SetPublishSingleFile(true)
            .SetProperty("DebugType", "embedded")
            .SetProperty("PublishDocumentationFiles", false)
            .SetProperty("IncludeAllContentForSelfExtract", true)
            .SetProperty("IncludeNativeLibrariesForSelfExtract", true)
            .EnableNoRestore();
    }

    /// <summary>
    /// Publishes a single-file executable beside the other publish artifacts.
    /// </summary>
    /// <param name="context">The successfully published runtime context.</param>
    protected virtual void CopySingleFileExecutable(PublishRidContext context)
    {
        if (!HasPackagingType(ApplicationPackagingType.DotNetSingleFile))
            return;

        Log.Information("Creating {fileName} single-file application bundle for {Rid}", GetSingleFileAssetPath(context).Name, context.RuntimeIdentifier);

        var temporaryDirectory = SingleFileInputsDirectory / Guid.NewGuid().ToString("N");
        temporaryDirectory.CreateDirectory();
        var singleFileInputs = CreateSingleFilePublishInputs(context);

        try
        {
            var settings = CreateSingleFilePublishSettings(context, temporaryDirectory)
                .SetProperty("FalloutBuildRuntimeManifest", singleFileInputs.ManifestPath)
                .SetProperty("FalloutBuildRuntimeManifestFileName", BuildRuntimeManifestFileName)
                .SetProperty("CustomAfterMicrosoftCommonTargets", singleFileInputs.TargetsPath);

            if (ConfigurePublishRid is not null)
            {
                settings = ConfigurePublishRid(settings, context)
                           ?? throw new InvalidOperationException("ConfigurePublishRid returned null.");
            }

            ExecuteDotNetPublish(settings);

            var executableName = GetPublishedExecutableName(context.RuntimeIdentifier);
            var sourcePath = temporaryDirectory / executableName;
            if (!sourcePath.FileExists())
            {
                throw new FileNotFoundException(
                    $"Published single-file executable '{sourcePath}' does not exist.",
                    sourcePath);
            }

            var targetPath = GetSingleFileAssetPath(context);
            sourcePath.Copy(targetPath, ExistsPolicy.FileOverwrite);

            var runtime = PublishRid.ParseRuntimeIdentifier(context.RuntimeIdentifier);
            if (runtime.Family is not PublishRidFamily.Windows)
            {
                UnixSystem.SetUnix755Executable(targetPath);
            }
        }
        finally
        {
            singleFileInputs.Delete();
            try
            {
                temporaryDirectory.DeleteDirectory();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Debug(exception, "Could not remove the temporary directory {Directory}.", temporaryDirectory);
            }
        }
    }

    private static AbsolutePath GetSingleFileAssetPath(PublishRidContext context)
    {
        var runtime = PublishRid.ParseRuntimeIdentifier(context.RuntimeIdentifier);
        var extension = runtime.Family is PublishRidFamily.Windows ? ".exe" : ".bin";
        return (AbsolutePath)$"{context.BundleOutputPath}{extension}";
    }

    private SingleFilePublishInputs CreateSingleFilePublishInputs(PublishRidContext context)
    {
        var directory = SingleFileInputsDirectory / Guid.NewGuid().ToString("N");
        directory.CreateDirectory();

        var manifestPath = directory / BuildRuntimeManifestFileName;
        var targetsPath = directory / "Fallout.SingleFile.targets";

        PublishUtilities.WriteRuntimeManifest(directory, BuildRuntimeManifestFileName,
            new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true,
                ApplicationPackagingType.DotNetSingleFile));

        var document = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("Content",
                        new XAttribute("Include", "$(FalloutBuildRuntimeManifest)"),
                        new XAttribute("Link", "$(FalloutBuildRuntimeManifestFileName)"),
                        new XElement("CopyToPublishDirectory", "PreserveNewest"),
                        new XElement("ExcludeFromSingleFile", false)))));
        document.Save(targetsPath);

        return new SingleFilePublishInputs(directory, manifestPath, targetsPath);
    }

    private sealed class SingleFilePublishInputs(
        AbsolutePath directory,
        AbsolutePath manifestPath,
        AbsolutePath targetsPath)
    {
        internal string ManifestPath { get; } = manifestPath;

        internal string TargetsPath { get; } = targetsPath;

        internal void Delete()
        {
            try
            {
                directory.DeleteDirectory();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Debug(exception, "Could not remove the temporary directory {Directory}.", directory);
            }
        }
    }
}
