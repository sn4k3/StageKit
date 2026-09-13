using Fallout.Common;
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
    private const int MacOSDmgCreateMaxAttempts = 3;

    /// <summary>
    /// The synthetic runtime identifier reported for multi-architecture macOS artifacts.
    /// </summary>
    public const string MultiArchMacOSRuntimeIdentifier = "osx-multiarch";

    /// <summary>
    /// Gets the code signing identity used to sign macOS applications and packages (e.g. 'Developer ID Application: Company (TEAMID)').
    /// If omitted and notarization is not configured, ad-hoc signing ('-') is used.
    /// </summary>
    [Parameter("macOS code signing identity (e.g. 'Developer ID Application: Company (TEAMID)'). Defaults to '-' (ad-hoc signing) if notarization is not configured.")]
    public string? MacSigningIdentity { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether macOS notarization should be performed.
    /// </summary>
    [Parameter("Enable macOS notarization for application bundles and packages. Defaults to true when notarization credentials are provided; otherwise false.")]
    public bool MacNotarize { get; protected set; }

    /// <summary>
    /// Gets the keychain profile name configured with 'xcrun notarytool store-credentials' for macOS notarization.
    /// </summary>
    [Parameter("Keychain profile name configured with 'xcrun notarytool store-credentials' for macOS notarization.")]
    public string? MacKeychainProfile { get; protected set; }

    /// <summary>
    /// Gets the Apple ID email used for macOS notarization with notarytool.
    /// </summary>
    [Parameter("Apple ID email used for macOS notarization with notarytool.")]
    public string? MacAppleId { get; protected set; }

    /// <summary>
    /// Gets the App Store Connect app-specific password used for macOS notarization with notarytool.
    /// </summary>
    [Parameter("App-specific password used for macOS notarization with notarytool.")]
    public string? MacPassword { get; protected set; }

    /// <summary>
    /// Gets the 10-character Apple Developer Team ID used for macOS notarization with notarytool.
    /// </summary>
    [Parameter("Apple Developer Team ID (10-character identifier) used for macOS notarization with notarytool.")]
    public string? MacTeamId { get; protected set; }

    /// <summary>
    /// Gets the path to the App Store Connect API private key (.p8) file used for macOS notarization.
    /// </summary>
    [Parameter("Path to the App Store Connect API private key file (.p8) used for macOS notarization.")]
    public AbsolutePath? MacApiKeyPath { get; protected set; }

    /// <summary>
    /// Gets the App Store Connect API key identifier used for macOS notarization.
    /// </summary>
    [Parameter("App Store Connect API key identifier used for macOS notarization.")]
    public string? MacApiKeyId { get; protected set; }

    /// <summary>
    /// Gets the App Store Connect API issuer UUID used for macOS notarization.
    /// </summary>
    [Parameter("App Store Connect API issuer UUID used for macOS notarization.")]
    public string? MacApiIssuerId { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether macOS notarization credentials are provided.
    /// </summary>
    protected virtual bool HasMacNotarizationCredentials =>
        !string.IsNullOrWhiteSpace(ResolvedMacKeychainProfile) ||
        (!string.IsNullOrWhiteSpace(ResolvedMacAppleId) &&
         !string.IsNullOrWhiteSpace(ResolvedMacPassword) &&
         !string.IsNullOrWhiteSpace(ResolvedMacTeamId)) ||
        (ResolvedMacApiKeyPath != null &&
         !string.IsNullOrWhiteSpace(ResolvedMacApiKeyId) &&
         !string.IsNullOrWhiteSpace(ResolvedMacApiIssuerId));

    /// <summary>
    /// Gets a value indicating whether real macOS notarization should be performed.
    /// </summary>
    protected virtual bool ShouldNotarizeMacOS => MacNotarize || HasMacNotarizationCredentials;

    private string? ResolvedMacKeychainProfile =>
        !string.IsNullOrWhiteSpace(MacKeychainProfile) ? MacKeychainProfile : MacAppBundleOptions.KeychainProfile;

    private string? ResolvedMacAppleId =>
        !string.IsNullOrWhiteSpace(MacAppleId) ? MacAppleId : MacAppBundleOptions.AppleId;

    private string? ResolvedMacPassword =>
        !string.IsNullOrWhiteSpace(MacPassword) ? MacPassword : MacAppBundleOptions.Password;

    private string? ResolvedMacTeamId =>
        !string.IsNullOrWhiteSpace(MacTeamId) ? MacTeamId : MacAppBundleOptions.TeamId;

    private AbsolutePath? ResolvedMacApiKeyPath =>
        MacApiKeyPath ?? MacAppBundleOptions.ApiKeyPath;

    private string? ResolvedMacApiKeyId =>
        !string.IsNullOrWhiteSpace(MacApiKeyId) ? MacApiKeyId : MacAppBundleOptions.ApiKeyId;

    private string? ResolvedMacApiIssuerId =>
        !string.IsNullOrWhiteSpace(MacApiIssuerId) ? MacApiIssuerId : MacAppBundleOptions.ApiIssuerId;

    private string? ResolvedMacSigningIdentity =>
        !string.IsNullOrWhiteSpace(MacSigningIdentity) ? MacSigningIdentity : MacAppBundleOptions.SigningIdentity;

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
        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var archivePath = (AbsolutePath)$"{context.BundleOutputPath}.zip";
        
        Log.Information("Creating and compressing {fileName} macOS application bundle for {Rid}",
            archivePath.Name, context.RuntimeIdentifier);

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
        var stagingPath = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var archivePath = (AbsolutePath)$"{GetMultiArchMacOSBundleOutputPath(x64Context)}.zip";

        Log.Information(
            "Creating and compressing {fileName} multi-architecture macOS application bundle for {X64Rid} and {Arm64Rid}",
            archivePath.Name, x64Context.RuntimeIdentifier, arm64Context.RuntimeIdentifier);

        
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

    /// <summary>Creates one compressed macOS disk image.</summary>
    protected virtual void CreateMacOSDmg(PublishRidContext context)
    {
        CreateMacOSPackage(context, ApplicationPackagingType.MacOSDmg, ".dmg",
            (appPath, outputPath) => MacPackage.GetDmgCommand(SoftwareName, appPath, outputPath));
    }

    /// <summary>Creates one macOS component installer package.</summary>
    protected virtual void CreateMacOSPkg(PublishRidContext context)
    {
        CreateMacOSPackage(context, ApplicationPackagingType.MacOSPkg, ".pkg",
            (appPath, outputPath) => MacPackage.GetPkgCommand(appPath, MacAppBundleOptions.BundleIdentifier,
                SoftwareVersion, outputPath));
    }

    /// <summary>Creates one multi-architecture compressed macOS disk image.</summary>
    protected virtual void CreateMultiArchMacOSDmg(PublishRidContext x64Context,
        PublishRidContext arm64Context)
    {
        CreateMultiArchMacOSPackage(x64Context, arm64Context, ApplicationPackagingType.MacOSDmg, ".dmg",
            (appPath, outputPath) => MacPackage.GetDmgCommand(SoftwareName, appPath, outputPath));
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
            ExecuteMacOSPackageCommand(createCommand(appPath, temporaryOutputPath), stagingPath,
                temporaryOutputPath, packagingType);
            if (IsMacOSHost && ShouldNotarizeMacOS)
            {
                NotarizeMacOSPackage(temporaryOutputPath);
            }
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
            ExecuteMacOSPackageCommand(createCommand(appPath, temporaryOutputPath), stagingPath,
                temporaryOutputPath, packagingType);
            if (IsMacOSHost && ShouldNotarizeMacOS)
            {
                NotarizeMacOSPackage(temporaryOutputPath);
            }
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

    private static AbsolutePath CreateTemporaryMacOSPackageOutputPath(AbsolutePath outputPath, string extension)
    {
        return outputPath.Parent / $".{outputPath.Name}.{Guid.NewGuid():N}{extension}";
    }

    internal void ExecuteMacOSPackageCommand(string command, AbsolutePath workingDirectory,
        AbsolutePath temporaryOutputPath, ApplicationPackagingType packagingType)
    {
        for (var attempt = 1;; attempt++)
        {
            try
            {
                ExecuteShell(command, workingDirectory);
                return;
            }
            catch (ProcessException exception) when (
                packagingType is ApplicationPackagingType.MacOSDmg &&
                attempt < MacOSDmgCreateMaxAttempts &&
                exception.Message.Contains("hdiutil: create failed - Resource busy",
                    StringComparison.OrdinalIgnoreCase))
            {
                temporaryOutputPath.DeleteFile();
                var retryDelay = TimeSpan.FromSeconds(attempt);
                Log.Warning(
                    "hdiutil could not create {OutputPath} because the resource was busy. Retrying in {DelaySeconds} second(s) (attempt {NextAttempt} of {MaxAttempts}).",
                    temporaryOutputPath, retryDelay.TotalSeconds, attempt + 1, MacOSDmgCreateMaxAttempts);
                Thread.Sleep(retryDelay);
            }
        }
    }

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
    /// Signs a completed macOS application bundle, applying real Developer ID signing and notarization when configured
    /// or falling back to ad-hoc signing.
    /// </summary>
    /// <param name="appPath">The application bundle path.</param>
    protected virtual void SignMacOSApp(AbsolutePath appPath)
    {
        if (!ShouldNotarizeMacOS && string.IsNullOrWhiteSpace(ResolvedMacSigningIdentity))
        {
            ExecuteCodeSign($"--force --deep --sign - {appPath.ToString().QuoteProcessArgument()}");
            return;
        }

        var identity = !string.IsNullOrWhiteSpace(ResolvedMacSigningIdentity)
            ? ResolvedMacSigningIdentity
            : "Developer ID Application";

        var entitlementsPath = appPath / "Contents" / $"{SoftwareName}.entitlements";
        var entitlementsArg = entitlementsPath.FileExists()
            ? $"--entitlements {entitlementsPath.ToString().QuoteProcessArgument()} "
            : string.Empty;

        ExecuteCodeSign($"--force --deep --timestamp --options runtime {entitlementsArg}--sign {identity.QuoteProcessArgument()} {appPath.ToString().QuoteProcessArgument()}");

        if (ShouldNotarizeMacOS)
        {
            NotarizeMacOSApp(appPath);
        }
    }

    /// <summary>
    /// Executes the macOS codesign command with the specified argument string.
    /// </summary>
    /// <param name="arguments">The codesign command-line arguments.</param>
    protected virtual void ExecuteCodeSign(string arguments)
    {
        using var process = ProcessTasks.StartProcess("codesign", arguments);
        process.AssertWaitForExit().AssertZeroExitCode();
    }

    /// <summary>
    /// Notarizes a signed macOS application bundle using xcrun notarytool and staples the ticket.
    /// </summary>
    /// <param name="appPath">The application bundle path.</param>
    protected virtual void NotarizeMacOSApp(AbsolutePath appPath)
    {
        var tempZip = PublishStagingDirectory / $"{Guid.NewGuid():N}.zip";
        try
        {
            ExecuteDittoZip(appPath, tempZip);
            NotarizeAndStapleArtifact(tempZip, appPath);
        }
        finally
        {
            tempZip.DeleteFile();
        }
    }

    /// <summary>
    /// Notarizes a macOS package (.dmg or .pkg) using xcrun notarytool and staples the ticket.
    /// </summary>
    /// <param name="packagePath">The package file path.</param>
    protected virtual void NotarizeMacOSPackage(AbsolutePath packagePath)
    {
        NotarizeAndStapleArtifact(packagePath, packagePath);
    }

    /// <summary>
    /// Submits an artifact to xcrun notarytool for notarization and staples the ticket to the target.
    /// </summary>
    /// <param name="submissionPath">The archive or package submitted to notarytool (.zip, .dmg, or .pkg).</param>
    /// <param name="stapleTarget">The target file or directory to which the ticket is stapled.</param>
    protected virtual void NotarizeAndStapleArtifact(AbsolutePath submissionPath, AbsolutePath stapleTarget)
    {
        var authArgs = GetNotaryToolAuthenticationArguments();
        Log.Information("Submitting {Artifact} for macOS notarization...", submissionPath.Name);
        ExecuteNotaryToolSubmit(submissionPath, authArgs);

        Log.Information("Stapling macOS notarization ticket to {Target}...", stapleTarget.Name);
        ExecuteStapler(stapleTarget);
    }

    /// <summary>
    /// Creates a zip archive using macOS ditto preserving resource forks and parent directory.
    /// </summary>
    /// <param name="source">The source bundle directory.</param>
    /// <param name="destinationZip">The destination zip archive path.</param>
    protected virtual void ExecuteDittoZip(AbsolutePath source, AbsolutePath destinationZip)
    {
        using var process = ProcessTasks.StartProcess("ditto",
            $"-c -k --keepParent {source.ToString().QuoteProcessArgument()} {destinationZip.ToString().QuoteProcessArgument()}");
        process.AssertWaitForExit().AssertZeroExitCode();
    }

    /// <summary>
    /// Submits an artifact to Apple's notarization service via xcrun notarytool.
    /// </summary>
    /// <param name="submissionPath">The archive or package submitted to notarytool.</param>
    /// <param name="authenticationArguments">The authentication argument fragment.</param>
    protected virtual void ExecuteNotaryToolSubmit(AbsolutePath submissionPath, string authenticationArguments)
    {
        using var process = ProcessTasks.StartProcess("xcrun",
            $"notarytool submit {submissionPath.ToString().QuoteProcessArgument()} {authenticationArguments} --wait");
        process.AssertWaitForExit().AssertZeroExitCode();
    }

    /// <summary>
    /// Staples a notarization ticket to a target application bundle or package via xcrun stapler.
    /// </summary>
    /// <param name="targetPath">The target application bundle or package.</param>
    protected virtual void ExecuteStapler(AbsolutePath targetPath)
    {
        using var process = ProcessTasks.StartProcess("xcrun",
            $"stapler staple {targetPath.ToString().QuoteProcessArgument()}");
        process.AssertWaitForExit().AssertZeroExitCode();
    }

    /// <summary>
    /// Gets the command-line argument fragment for authenticating with xcrun notarytool.
    /// </summary>
    protected virtual string GetNotaryToolAuthenticationArguments()
    {
        var keychainProfile = ResolvedMacKeychainProfile;
        if (!string.IsNullOrWhiteSpace(keychainProfile))
        {
            return $"--keychain-profile {keychainProfile.QuoteProcessArgument()}";
        }

        var appleId = ResolvedMacAppleId;
        var password = ResolvedMacPassword;
        var teamId = ResolvedMacTeamId;

        if (!string.IsNullOrWhiteSpace(appleId) ||
            !string.IsNullOrWhiteSpace(password) ||
            !string.IsNullOrWhiteSpace(teamId))
        {
            if (string.IsNullOrWhiteSpace(appleId) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(teamId))
            {
                throw new InvalidOperationException(
                    "macOS notarization using Apple ID requires MacAppleId, MacPassword, and MacTeamId to all be specified.");
            }

            return $"--apple-id {appleId.QuoteProcessArgument()} " +
                   $"--password {password.QuoteProcessArgument()} " +
                   $"--team-id {teamId.QuoteProcessArgument()}";
        }

        var apiKeyPath = ResolvedMacApiKeyPath;
        var apiKeyId = ResolvedMacApiKeyId;
        var apiIssuerId = ResolvedMacApiIssuerId;

        if (apiKeyPath != null ||
            !string.IsNullOrWhiteSpace(apiKeyId) ||
            !string.IsNullOrWhiteSpace(apiIssuerId))
        {
            if (apiKeyPath == null ||
                string.IsNullOrWhiteSpace(apiKeyId) ||
                string.IsNullOrWhiteSpace(apiIssuerId))
            {
                throw new InvalidOperationException(
                    "macOS notarization using API key requires MacApiKeyPath, MacApiKeyId, and MacApiIssuerId to all be specified.");
            }

            return $"--key {apiKeyPath.ToString().QuoteProcessArgument()} " +
                   $"--key-id {apiKeyId.QuoteProcessArgument()} " +
                   $"--issuer {apiIssuerId.QuoteProcessArgument()}";
        }

        throw new InvalidOperationException(
            "macOS notarization was requested, but notarization credentials were not configured. Specify MacKeychainProfile, or MacAppleId + MacPassword + MacTeamId, or MacApiKeyPath + MacApiKeyId + MacApiIssuerId.");
    }
}
