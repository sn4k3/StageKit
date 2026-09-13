using Fallout.Solutions;
using Serilog;
using StageKit.Fallout;
using StageKit.Runtime;

internal class Build : StageKitBuild
{
    public Build()
    {
        BeforePublishRid = context =>
            Log.Information("Publishing {Rid} to {Path}",
                context.RuntimeIdentifier, context.PublishPath);

        // In this case, remove these tokens to MainProject to detect
        ExcludedProjectNameTokens.Remove("demo");

        // Set default packaging formats to
        PackagingTypes =
        [
            ApplicationPackagingType.Portable,
            ApplicationPackagingType.WindowsInstaller,
            ApplicationPackagingType.LinuxDeb,
            ApplicationPackagingType.LinuxRpm,
            ApplicationPackagingType.LinuxArchPackage,
            ApplicationPackagingType.LinuxAppImage,
            ApplicationPackagingType.LinuxFlatpak,
            ApplicationPackagingType.LinuxSnap,
            ApplicationPackagingType.MacOSAppBundle,
            ApplicationPackagingType.MacOSDmg,
            ApplicationPackagingType.MacOSPkg,
            ApplicationPackagingType.DotNetSingleFile
        ];
        
        FileAssociations.UnionWith([
            new FileAssociation(".skini", "StageKit SKINI File", "application/x-stagekit-skini"),
            new FileAssociation(".skjson", "StageKit SKJSON File", "application/x-stagekit-skjson"),
        ]);
    }

    /// <summary>
    /// Gets the product name, which differs from the solution name.
    /// </summary>
    public override string SoftwareName => SolutionName;

    protected override WindowsInstallerOptions CreateWindowsInstallerOptions()
    {
        var options = base.CreateWindowsInstallerOptions();
        options.SyncContextMenuOpenWithFileAssociations();
        return options;
    }

    /// <inheritdoc />
    protected override LinuxAppBundleOptions CreateLinuxAppBundleOptions()
    {
        var options = base.CreateLinuxAppBundleOptions();
        options.SnapStagePackages.Add("libfontconfig1");
        return options;
    }

    /// <inheritdoc />
    protected override MacAppBundleOptions CreateMacAppBundleOptions()
    {
        var options = base.CreateMacAppBundleOptions();
        options.ApplicationCategory = "public.app-category.developer-tools";
        return options;
    }

    public static int Main()
    {
        return Execute<Build>(x => x.Compile);
    }
}
