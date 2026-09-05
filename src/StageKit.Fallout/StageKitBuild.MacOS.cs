using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Serilog;
using StageKit.Primitives;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;
using StageKit.Runtime;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// The synthetic runtime identifier reported for multi-architecture macOS artifacts.
    /// </summary>
    public const string MultiArchMacOSRuntimeIdentifier = "osx-multiarch";

    /// <summary>
    /// Selects and creates the configured macOS application bundles.
    /// </summary>
    /// <param name="contexts">The successfully published runtime contexts.</param>
    protected virtual void CreateMacOSApps(IReadOnlyCollection<PublishRidContext> contexts)
    {
        var macContexts = contexts
            .Where(context => PublishRid.ParseRuntimeIdentifier(context.RuntimeIdentifier).Family
                is PublishRidFamily.MacOS)
            .ToArray();
        if (macContexts.Length == 0)
            return;

        if (!IsUnixHost)
        {
            WarnMacOSAppsUnsupportedHost();
            return;
        }

        if (!PublishMultiArch)
        {
            foreach (var context in macContexts)
                CreateMacOSApp(context);

            return;
        }

        var x64Context = macContexts.FirstOrDefault(context =>
            context.RuntimeIdentifier.Equals("osx-x64", StringComparison.OrdinalIgnoreCase));
        var arm64Context = macContexts.FirstOrDefault(context =>
            context.RuntimeIdentifier.Equals("osx-arm64", StringComparison.OrdinalIgnoreCase));
        if (x64Context is null || arm64Context is null)
        {
            throw new InvalidOperationException(
                "A multi-architecture macOS app requires both 'osx-x64' and 'osx-arm64' publish outputs.");
        }

        CreateMultiArchMacOSApp(x64Context, arm64Context);
    }

    /// <summary>
    /// Logs that macOS application bundles cannot be created on the current host.
    /// </summary>
    protected virtual void WarnMacOSAppsUnsupportedHost()
    {
        Log.Warning("Skipping macOS application bundles on a non-Unix host.");
    }

    /// <summary>
    /// Selects and creates the configured native macOS distribution packages.
    /// </summary>
    /// <param name="contexts">The successfully published runtime contexts.</param>
    protected virtual void CreateMacOSPackages(IReadOnlyCollection<PublishRidContext> contexts)
    {
        var macContexts = contexts
            .Where(context => PublishRid.ParseRuntimeIdentifier(context.RuntimeIdentifier).Family
                is PublishRidFamily.MacOS)
            .ToArray();
        if (macContexts.Length == 0)
            return;

        if (!IsMacOSHost)
        {
            WarnMacOSPackagesUnsupportedHost();
            return;
        }

        if (!PublishMultiArch)
        {
            foreach (var context in macContexts)
            {
                if (HasPackagingType(ApplicationPackagingType.MacOSDmg))
                    CreateMacOSDmg(context);
                if (HasPackagingType(ApplicationPackagingType.MacOSPkg))
                    CreateMacOSPkg(context);
            }

            return;
        }

        var x64Context = macContexts.FirstOrDefault(context =>
            context.RuntimeIdentifier.Equals("osx-x64", StringComparison.OrdinalIgnoreCase));
        var arm64Context = macContexts.FirstOrDefault(context =>
            context.RuntimeIdentifier.Equals("osx-arm64", StringComparison.OrdinalIgnoreCase));
        if (x64Context is null || arm64Context is null)
        {
            throw new InvalidOperationException(
                "A multi-architecture macOS package requires both 'osx-x64' and 'osx-arm64' publish outputs.");
        }

        if (HasPackagingType(ApplicationPackagingType.MacOSDmg))
            CreateMultiArchMacOSDmg(x64Context, arm64Context);
        if (HasPackagingType(ApplicationPackagingType.MacOSPkg))
            CreateMultiArchMacOSPkg(x64Context, arm64Context);
    }

    /// <summary>
    /// Logs that native macOS packages cannot be created on the current host.
    /// </summary>
    protected virtual void WarnMacOSPackagesUnsupportedHost()
    {
        Log.Warning("Skipping macOS DMG and PKG packages on a non-macOS host.");
    }

    /// <summary>
    /// Creates the common macOS application directory structure and metadata.
    /// </summary>
    /// <param name="stagingPath">The isolated staging directory.</param>
    /// <returns>The staged application bundle path.</returns>
    /// <exception cref="FileNotFoundException">The configured macOS icon does not exist.</exception>
    protected virtual AbsolutePath CreateMacOSAppLayout(AbsolutePath stagingPath)
    {
        if (!MacOSIconFile.FileExists())
        {
            throw new FileNotFoundException(
                $"The configured macOS application icon '{MacOSIconFile}' does not exist.",
                MacOSIconFile);
        }

        var options = MacAppBundleOptions;
        ResolveMacOSPathOptions(options);
        var appPath = stagingPath / $"{SoftwareName}.app";
        var contentsPath = appPath / "Contents";
        var executablePath = contentsPath / "MacOS";
        var resourcesPath = contentsPath / "Resources";
        executablePath.CreateDirectory();
        resourcesPath.CreateDirectory();

        var iconFileName = options.IconFileName!;
        MacOSIconFile.Copy(resourcesPath / iconFileName, ExistsPolicy.FileOverwrite);
        (contentsPath / "Info.plist").WriteAllText(
            MacAppBundle.GetInfoPList(options).ReplaceLineEndings("\n"));
        (contentsPath / $"{SoftwareName}.entitlements").WriteAllText(
            MacAppBundle.GetEntitlements(options).ReplaceLineEndings("\n"));

        return appPath;
    }

    /// <summary>
    /// Creates one single-architecture macOS application archive.
    /// </summary>
    /// <param name="context">The macOS runtime publish context.</param>
    protected virtual void CreateMacOSApp(PublishRidContext context)
    {
        Log.Information("Creating and compressing macOS application bundle for {Rid}",
            context.RuntimeIdentifier);

        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var archivePath = (AbsolutePath)$"{context.BundleOutputPath}.zip";

        try
        {
            stagingPath.DeleteDirectory();
            StageMacOSApp(context, stagingPath, ApplicationPackagingType.MacOSAppBundle);
            archivePath.DeleteFile();
            PublishUtilities.CreateZip(stagingPath, archivePath);
        }
        finally
        {
            stagingPath.DeleteDirectory();
        }
    }

    /// <summary>
    /// Creates one multi-architecture macOS application archive.
    /// </summary>
    /// <param name="x64Context">The Intel macOS runtime publish context.</param>
    /// <param name="arm64Context">The Apple Silicon macOS runtime publish context.</param>
    protected virtual void CreateMultiArchMacOSApp(PublishRidContext x64Context,
        PublishRidContext arm64Context)
    {
        Log.Information(
            "Creating and compressing multi-architecture macOS application bundle for {X64Rid} and {Arm64Rid}",
            x64Context.RuntimeIdentifier, arm64Context.RuntimeIdentifier);

        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var archivePath = (AbsolutePath)$"{GetMultiArchMacOSBundleOutputPath(x64Context)}.zip";

        try
        {
            stagingPath.DeleteDirectory();
            StageMultiArchMacOSApp(x64Context, arm64Context, stagingPath,
                ApplicationPackagingType.MacOSAppBundle);
            archivePath.DeleteFile();
            PublishUtilities.CreateZip(stagingPath, archivePath);
        }
        finally
        {
            stagingPath.DeleteDirectory();
        }
    }

    /// <summary>Creates one read-only macOS disk image.</summary>
    protected virtual void CreateMacOSDmg(PublishRidContext context)
    {
        CreateMacOSPackage(context, ApplicationPackagingType.MacOSDmg, ".dmg",
            (appPath, outputPath) => MacPackage.GetDmgCommand(
                SoftwareName, appPath.Parent, appPath.Parent / "Applications", outputPath));
    }

    /// <summary>Creates one macOS component installer package.</summary>
    protected virtual void CreateMacOSPkg(PublishRidContext context)
    {
        CreateMacOSPackage(context, ApplicationPackagingType.MacOSPkg, ".pkg",
            (appPath, outputPath) => MacPackage.GetPkgCommand(appPath, MacAppBundleOptions.BundleIdentifier,
                SoftwareVersion, outputPath));
    }

    /// <summary>Creates one multi-architecture read-only macOS disk image.</summary>
    protected virtual void CreateMultiArchMacOSDmg(PublishRidContext x64Context,
        PublishRidContext arm64Context)
    {
        CreateMultiArchMacOSPackage(x64Context, arm64Context, ApplicationPackagingType.MacOSDmg, ".dmg",
            (appPath, outputPath) => MacPackage.GetDmgCommand(
                SoftwareName, appPath.Parent, appPath.Parent / "Applications", outputPath));
    }

    /// <summary>Creates one multi-architecture macOS component installer package.</summary>
    protected virtual void CreateMultiArchMacOSPkg(PublishRidContext x64Context,
        PublishRidContext arm64Context)
    {
        CreateMultiArchMacOSPackage(x64Context, arm64Context, ApplicationPackagingType.MacOSPkg, ".pkg",
            (appPath, outputPath) => MacPackage.GetPkgCommand(appPath, MacAppBundleOptions.BundleIdentifier,
                SoftwareVersion, outputPath));
    }

    private void CreateMacOSPackage(PublishRidContext context, ApplicationPackagingType packagingType,
        string extension, Func<AbsolutePath, AbsolutePath, string> createCommand)
    {
        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var outputPath = (AbsolutePath)$"{context.BundleOutputPath}{extension}";
        var temporaryOutputPath = CreateTemporaryMacOSPackageOutputPath(outputPath, extension);
        try
        {
            stagingPath.DeleteDirectory();
            var appPath = StageMacOSApp(context, stagingPath, packagingType);
            ExecuteShell(createCommand(appPath, temporaryOutputPath), stagingPath);
            MoveMacOSPackageOutput(temporaryOutputPath, outputPath, extension);
        }
        finally
        {
            temporaryOutputPath.DeleteFile();
            stagingPath.DeleteDirectory();
        }
    }

    private void CreateMultiArchMacOSPackage(PublishRidContext x64Context, PublishRidContext arm64Context,
        ApplicationPackagingType packagingType, string extension,
        Func<AbsolutePath, AbsolutePath, string> createCommand)
    {
        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var outputPath = (AbsolutePath)$"{GetMultiArchMacOSBundleOutputPath(x64Context)}{extension}";
        var temporaryOutputPath = CreateTemporaryMacOSPackageOutputPath(outputPath, extension);
        try
        {
            stagingPath.DeleteDirectory();
            var appPath = StageMultiArchMacOSApp(x64Context, arm64Context, stagingPath, packagingType);
            ExecuteShell(createCommand(appPath, temporaryOutputPath), stagingPath);
            MoveMacOSPackageOutput(temporaryOutputPath, outputPath, extension);
        }
        finally
        {
            temporaryOutputPath.DeleteFile();
            stagingPath.DeleteDirectory();
        }
    }

    private AbsolutePath StageMacOSApp(PublishRidContext context, AbsolutePath stagingPath,
        ApplicationPackagingType packagingType)
    {
        var appPath = CreateMacOSAppLayout(stagingPath);
        var executablePath = appPath / "Contents" / "MacOS";
        var executableName = MacAppBundleOptions.ExecutableName!;
        ValidateMacOSPayload(context, executableName);
        context.PublishPath.Copy(executablePath, ExistsPolicy.MergeAndOverwrite);
        UnixSystem.SetUnix755Executable(executablePath / executableName);
        PublishUtilities.WriteRuntimeManifest(executablePath, BuildRuntimeManifestFileName,
            new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true, packagingType));

        if (IsMacOSHost)
            SignMacOSApp(appPath);

        return appPath;
    }

    private AbsolutePath StageMultiArchMacOSApp(PublishRidContext x64Context, PublishRidContext arm64Context,
        AbsolutePath stagingPath, ApplicationPackagingType packagingType)
    {
        var appPath = CreateMacOSAppLayout(stagingPath);
        var executablePath = appPath / "Contents" / "MacOS";
        var options = MacAppBundleOptions;
        var executableName = options.ExecutableName!;
        var runtimePayloads = new[]
        {
            (Context: x64Context, DirectoryName: options.X64RuntimeIdentifier),
            (Context: arm64Context, DirectoryName: options.Arm64RuntimeIdentifier)
        };

        foreach (var payload in runtimePayloads)
            ValidateMacOSPayload(payload.Context, executableName);

        foreach (var payload in runtimePayloads)
        {
            var runtimePath = executablePath / payload.DirectoryName;
            payload.Context.PublishPath.Copy(runtimePath, ExistsPolicy.MergeAndOverwrite);
            UnixSystem.SetUnix755Executable(runtimePath / executableName);
            PublishUtilities.WriteRuntimeManifest(runtimePath, BuildRuntimeManifestFileName,
                new BuildRuntime(MultiArchMacOSRuntimeIdentifier, SoftwareVersion, true, packagingType));
        }

        var launcherPath = executablePath / executableName;
        launcherPath.WriteAllText(MacAppBundle.GetMultiArchEntryScript(options).ReplaceLineEndings("\n"));
        UnixSystem.SetUnix755Executable(launcherPath);

        if (IsMacOSHost)
            SignMacOSApp(appPath);

        return appPath;
    }

    private static AbsolutePath CreateTemporaryMacOSPackageOutputPath(AbsolutePath outputPath, string extension) =>
        outputPath.Parent / $".{outputPath.Name}.{Guid.NewGuid():N}{extension}";

    private static void MoveMacOSPackageOutput(AbsolutePath temporaryOutputPath, AbsolutePath outputPath,
        string extension)
    {
        if (!temporaryOutputPath.FileExists())
        {
            throw new FileNotFoundException(
                $"macOS {extension} packaging did not produce '{temporaryOutputPath}'.", temporaryOutputPath);
        }

        temporaryOutputPath.Move(outputPath, ExistsPolicy.FileOverwrite);
    }

    /// <summary>
    /// Resolves the base output path for the multi-architecture macOS artifact through <see cref="AssetName"/>.
    /// </summary>
    /// <param name="context">Any macOS runtime publish context taking part in the bundle.</param>
    /// <returns>The validated base output path, without an extension.</returns>
    protected virtual AbsolutePath GetMultiArchMacOSBundleOutputPath(PublishRidContext context)
    {
        var assetContext = context with { RuntimeIdentifier = MultiArchMacOSRuntimeIdentifier };
        var assetName = FileUtilities.ValidatePathLeafName(AssetName(assetContext), nameof(AssetName));
        return PublishUtilities.GetDirectChildPath(PublishDirectory, assetName, nameof(AssetName));
    }

    private void ResolveMacOSPathOptions(MacAppBundleOptions options)
    {
        options.IconFileName = FileUtilities.ValidatePathLeafName(
            options.IconFileName ?? $"{options.ProductName}.icns",
            $"{nameof(MacAppBundleOptions)}.{nameof(MacAppBundleOptions.IconFileName)}");

        var executableName = FileUtilities.ValidatePathLeafName(
            options.ExecutableName ?? options.ProductName,
            $"{nameof(MacAppBundleOptions)}.{nameof(MacAppBundleOptions.ExecutableName)}");
        options.ExecutableName = executableName;

        if (!PublishMultiArch)
            return;

        var x64RuntimeIdentifier = FileUtilities.ValidatePathLeafName(
            options.X64RuntimeIdentifier,
            $"{nameof(MacAppBundleOptions)}.{nameof(MacAppBundleOptions.X64RuntimeIdentifier)}");
        var arm64RuntimeIdentifier = FileUtilities.ValidatePathLeafName(
            options.Arm64RuntimeIdentifier,
            $"{nameof(MacAppBundleOptions)}.{nameof(MacAppBundleOptions.Arm64RuntimeIdentifier)}");

        if (x64RuntimeIdentifier.Equals(arm64RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{nameof(MacAppBundleOptions.X64RuntimeIdentifier)} and " +
                $"{nameof(MacAppBundleOptions.Arm64RuntimeIdentifier)} must be distinct.");
        }

        if (executableName.Equals(x64RuntimeIdentifier, StringComparison.OrdinalIgnoreCase) ||
            executableName.Equals(arm64RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{nameof(MacAppBundleOptions.ExecutableName)} must be distinct from the runtime directory names.");
        }
    }

    private static void ValidateMacOSPayload(PublishRidContext context, string executableName)
    {
        var executablePath = context.PublishPath / executableName;
        if (!executablePath.FileExists())
        {
            throw new FileNotFoundException(
                $"Published executable '{executablePath}' does not exist for '{context.RuntimeIdentifier}'.",
                executablePath);
        }
    }

    /// <summary>
    /// Ad-hoc signs a completed macOS application bundle.
    /// </summary>
    /// <param name="appPath">The application bundle path.</param>
    protected virtual void SignMacOSApp(AbsolutePath appPath)
    {
        using var process = ProcessTasks.StartProcess("codesign",
            $"--force --deep --sign - {appPath.ToString().QuoteProcessArgument()}");
        process.AssertWaitForExit();
    }
}
