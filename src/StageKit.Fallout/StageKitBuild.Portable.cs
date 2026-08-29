using Fallout.Common.IO;
using Serilog;
using StageKit.Runtime;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Determines whether a runtime identifier still needs a portable ZIP.
    /// </summary>
    /// <param name="runtimeIdentifier">The runtime identifier to inspect.</param>
    /// <returns>
    /// <c>false</c> when a macOS application bundle already ships that runtime as a ZIP; otherwise, <c>true</c>.
    /// </returns>
    protected virtual bool ShouldCreatePortableZip(string runtimeIdentifier)
    {
        return PublishRid.ParseRuntimeIdentifier(runtimeIdentifier).Family is not PublishRidFamily.MacOS ||
               !PublishBundles.HasFlag(ApplicationPackagingType.MacOSAppBundle);
    }

    /// <summary>
    /// Creates a portable ZIP from one published runtime output.
    /// </summary>
    /// <param name="context">The runtime publish context.</param>
    internal virtual void CreatePortableZip(PublishRidContext context)
    {
        Log.Information("Compressing portable application bundle for {Rid}", context.RuntimeIdentifier);

        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var archivePath = (AbsolutePath)$"{context.BundleOutputPath}.zip";

        try
        {
            stagingPath.DeleteDirectory();
            context.PublishPath.Copy(stagingPath, ExistsPolicy.MergeAndOverwrite);
            PublishUtilities.WriteRuntimeManifest(stagingPath, BuildRuntimeManifestFileName,
                new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, false, ApplicationPackagingType.Portable));

            archivePath.DeleteFile();
            PublishUtilities.CreateZip(stagingPath, archivePath);
        }
        finally
        {
            stagingPath.DeleteDirectory();
        }
    }
}