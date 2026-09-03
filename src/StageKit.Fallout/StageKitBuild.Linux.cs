using System.Runtime.InteropServices;
using Fallout.Common.IO;
using Serilog;
using StageKit.Primitives;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;
using StageKit.Runtime;

namespace StageKit.Fallout;

public partial class StageKitBuild
{
    /// <summary>
    /// Gets a value indicating whether FUSE 2 is available on the current Linux host.
    /// </summary>
    protected virtual bool IsFuseAvailable => LinuxSystem.IsFuseAvailable;

    /// <summary>
    /// Gets the current process architecture used to select appimagetool.
    /// </summary>
    protected virtual Architecture HostArchitecture => RuntimeInformation.ProcessArchitecture;

    /// <summary>
    /// Gets the persistent appimagetool download and extraction cache directory.
    /// </summary>
    protected virtual AbsolutePath AppImageToolCacheDirectory => TemporaryDirectory / "appimagetool";

    /// <summary>
    /// Gets the parent directory for isolated AppDir staging directories.
    /// </summary>
    protected virtual AbsolutePath AppImageStagingDirectory => PublishStagingDirectory;

    /// <summary>
    /// Gets the parent directory for Debian package staging directories.
    /// </summary>
    /// <remarks>
    /// Debian staging uses the operating system's temporary filesystem so Unix permissions remain effective when the
    /// repository is located on a mounted Windows filesystem under WSL.
    /// </remarks>
    protected virtual AbsolutePath DebianPackageStagingDirectory =>
        Path.Combine(Path.GetTempPath(), "stagekit-fallout", "debian");

    /// <summary>
    /// Composes the shell command that extracts one downloaded appimagetool AppImage.
    /// </summary>
    /// <param name="downloadedPath">The downloaded AppImage path.</param>
    /// <returns>The shell-safe extraction command.</returns>
    protected virtual string CreateAppImageToolExtractionCommand(AbsolutePath downloadedPath)
    {
        return $"{downloadedPath.ToString().QuoteShell()} --appimage-extract";
    }

    /// <summary>
    /// Selects and creates the configured Linux AppImage bundles.
    /// </summary>
    /// <param name="contexts">The successfully published runtime contexts.</param>
    protected virtual void CreateLinuxAppImages(IReadOnlyCollection<PublishRidContext> contexts)
    {
        var linuxContexts = contexts
            .Select(context => (Context: context,
                Runtime: PublishRid.ParseRuntimeIdentifier(context.RuntimeIdentifier)))
            .Where(item => item.Runtime.Family is PublishRidFamily.Linux)
            .ToArray();
        if (linuxContexts.Length == 0)
            return;

        if (!IsLinuxHost)
        {
            WarnLinuxAppImagesUnsupportedHost();
            return;
        }

        foreach (var item in linuxContexts)
            CreateLinuxAppImage(item.Context, item.Runtime.AppImageArchitecture!);
    }

    /// <summary>
    /// Creates one Linux AppImage from a published runtime output.
    /// </summary>
    /// <param name="context">The Linux runtime publish context.</param>
    /// <param name="architecture">The AppImage target architecture.</param>
    protected virtual void CreateLinuxAppImage(PublishRidContext context, string architecture)
    {
        Log.Information("Creating Linux AppImage bundle for {Rid} ({Architecture})",
            context.RuntimeIdentifier, architecture);

        var appDirPath = AppImageStagingDirectory / Guid.NewGuid().ToString("N");
        var outputPath = (AbsolutePath)$"{context.BundleOutputPath}.AppImage";
        var temporaryOutputPath = CreateTemporaryAppImageOutputPath(outputPath);

        try
        {
            DeleteFileSystemEntry(appDirPath);
            DeleteFileSystemEntry(temporaryOutputPath);
            CreateLinuxAppDir(context, appDirPath);
            var appImageTool = PrepareAppImageTool();

            ExecuteShell(
                CreateAppImageBuildCommand(architecture, appImageTool, appDirPath, temporaryOutputPath),
                appDirPath);
            if (!temporaryOutputPath.FileExists())
            {
                throw new FileNotFoundException(
                    $"Temporary AppImage output '{temporaryOutputPath}' does not exist.",
                    temporaryOutputPath);
            }

            UnixSystem.SetUnix755Executable(temporaryOutputPath);
            temporaryOutputPath.Move(outputPath, ExistsPolicy.FileOverwrite);
        }
        finally
        {
            try
            {
                DeleteFileSystemEntry(temporaryOutputPath);
            }
            finally
            {
                DeleteFileSystemEntry(appDirPath);
            }
        }
    }

    /// <summary>
    /// Creates the requested Linux distribution packages for each Linux runtime.
    /// </summary>
    protected virtual void CreateLinuxPackages(IReadOnlyCollection<PublishRidContext> contexts)
    {
        var linuxContexts = contexts
            .Select(context =>
                (Context: context, Runtime: PublishRid.ParseRuntimeIdentifier(context.RuntimeIdentifier)))
            .Where(item => item.Runtime.Family is PublishRidFamily.Linux)
            .ToArray();
        if (linuxContexts.Length == 0)
            return;

        if (!IsLinuxHost)
        {
            Log.Warning("Skipping Linux distribution packages on a non-Linux host.");
            return;
        }

        foreach (var item in linuxContexts)
        {
            if (HasPackagingType(ApplicationPackagingType.LinuxFlatpak))
                CreateLinuxFlatpak(item.Context, item.Runtime.Architecture);
            if (HasPackagingType(ApplicationPackagingType.LinuxDeb))
                CreateLinuxDeb(item.Context, item.Runtime.Architecture);
            if (HasPackagingType(ApplicationPackagingType.LinuxRpm))
                CreateLinuxRpm(item.Context, item.Runtime.Architecture);
            if (HasPackagingType(ApplicationPackagingType.LinuxArchPackage))
                CreateLinuxArchPackage(item.Context, item.Runtime.Architecture);
            if (HasPackagingType(ApplicationPackagingType.LinuxSnap))
                CreateLinuxSnap(item.Context, item.Runtime.Architecture);
        }
    }

    /// <summary>Creates one Flatpak bundle using <c>flatpak-builder</c>.</summary>
    protected virtual void CreateLinuxFlatpak(PublishRidContext context, string architecture)
    {
        if (!CanBuildFlatpakArchitecture(architecture))
        {
            Log.Warning("Skipping Flatpak bundle for {Rid}: target architecture {Architecture} is not native to the Linux host.",
                context.RuntimeIdentifier, architecture);
            return;
        }

        var options = LinuxAppBundleOptions;
        ResolveLinuxPathOptions(options);
        var staging = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var source = staging / options.ProductName;
        var buildDirectory = staging / "build";
        var repository = staging / "repo";
        var manifest = staging / $"{options.ApplicationId}.yml";
        var output = (AbsolutePath)$"{context.BundleOutputPath}.flatpak";
        var temporaryOutput = CreateTemporaryPackageOutputPath(output);
        try
        {
            staging.CreateOrCleanDirectory();
            source.CreateDirectory();
            context.PublishPath.Copy(source, ExistsPolicy.MergeAndOverwrite);
            PublishUtilities.WriteRuntimeManifest(source, BuildRuntimeManifestFileName,
                new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true,
                    ApplicationPackagingType.LinuxFlatpak));
            var share = source / "share";
            (share / "applications").CreateDirectory();
            (share / "applications" / $"{options.ApplicationId}.desktop").WriteAllText(
                LinuxAppBundle.GetDesktopEntry(options, options.ExecutableName, options.ApplicationId));
            (share / "metainfo").CreateDirectory();
            (share / "metainfo" / $"{options.ApplicationId}.appdata.xml").WriteAllText(
                LinuxAppBundle.GetAppStreamMetadata(options));
            var icon = LinuxIconFile;
            ValidateLinuxIcon(icon);
            var iconDirectory = share / "icons" / "hicolor" /
                                (icon.Extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
                                    ? "scalable"
                                    : "256x256") / "apps";
            iconDirectory.CreateDirectory();
            icon.Copy(iconDirectory / $"{options.ApplicationId}{icon.Extension}", ExistsPolicy.FileOverwrite);
            manifest.WriteAllText(LinuxAppBundle.GetFlatpakManifest(options));
            var flatpakArchitecture = GetFlatpakArchitecture(architecture);
            ExecuteShell(CreateFlatpakBuilderCommand(flatpakArchitecture, repository, buildDirectory, manifest),
                staging);
            ExecuteShell(CreateFlatpakBundleCommand(flatpakArchitecture, repository, temporaryOutput,
                options.ApplicationId), staging);
            MovePackageOutput(temporaryOutput, output, "Flatpak");
        }
        finally
        {
            temporaryOutput.DeleteFile();
            DeleteFileSystemEntry(staging);
        }
    }

    /// <summary>Creates one Debian package using <c>dpkg-deb</c>.</summary>
    protected virtual void CreateLinuxDeb(PublishRidContext context, string architecture)
    {
        var options = LinuxAppBundleOptions;
        ResolveLinuxPathOptions(options);
        var packageName = GetLinuxPackageName();
        var staging = DebianPackageStagingDirectory / Guid.NewGuid().ToString("N");
        var output = (AbsolutePath)$"{context.BundleOutputPath}.deb";
        var temporaryOutput = CreateTemporaryPackageOutputPath(output);
        try
        {
            var root = staging / "root";
            CreateLinuxPackageRoot(context, options, root, ApplicationPackagingType.LinuxDeb);
            var control = root / "DEBIAN";
            control.CreateDirectory();
            var controlFile = control / "control";
            controlFile.WriteAllText(LinuxPackage.GetDebianControl(packageName,
                LinuxPackage.GetDebianVersion(SoftwareVersion), GetDebArchitecture(architecture),
                options.DebPackageMaintainer, options.Summary, options.Description));
            SetDebianControlPermissions(control, controlFile);
            ExecuteShell($"dpkg-deb --build {root.ToString().QuoteShell()} {temporaryOutput.ToString().QuoteShell()}",
                staging);
            MovePackageOutput(temporaryOutput, output, "Debian");
        }
        finally
        {
            temporaryOutput.DeleteFile();
            DeleteFileSystemEntry(staging);
        }
    }

    /// <summary>Creates one RPM package using <c>rpmbuild</c>.</summary>
    protected virtual void CreateLinuxRpm(PublishRidContext context, string architecture)
    {
        var options = LinuxAppBundleOptions;
        ResolveLinuxPathOptions(options);
        var packageName = GetLinuxPackageName();
        var staging = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var top = staging / "rpmbuild";
        var payload = staging / "payload";
        var spec = top / "SPECS" / $"{packageName}.spec";
        var output = (AbsolutePath)$"{context.BundleOutputPath}.rpm";
        var temporaryOutput = CreateTemporaryPackageOutputPath(output);
        try
        {
            CreateLinuxPackageRoot(context, options, payload, ApplicationPackagingType.LinuxRpm);
            (top / "SPECS").CreateDirectory();
            (top / "RPMS").CreateDirectory();
            var rpmVersion = LinuxPackage.GetRpmVersion(SoftwareVersion);
            spec.WriteAllText(LinuxPackage.GetRpmSpec(packageName, rpmVersion,
                GetRpmArchitecture(architecture), options.License, options.Summary, options.Description, payload));
            ExecuteShell(CreateRpmBuildCommand(top, spec), staging);
            var rpm = Directory.GetFiles(top, "*.rpm", SearchOption.AllDirectories).SingleOrDefault()
                      ?? throw new FileNotFoundException("rpmbuild did not produce exactly one RPM package.");
            ((AbsolutePath)rpm).Move(temporaryOutput, ExistsPolicy.FileOverwrite);
            MovePackageOutput(temporaryOutput, output, "RPM");
        }
        finally
        {
            temporaryOutput.DeleteFile();
            DeleteFileSystemEntry(staging);
        }
    }

    /// <summary>Creates one Arch Linux binary package using <c>makepkg</c>.</summary>
    protected virtual void CreateLinuxArchPackage(PublishRidContext context, string architecture)
    {
        var options = LinuxAppBundleOptions;
        ResolveLinuxPathOptions(options);
        var packageName = GetLinuxPackageName();
        var archVersion = LinuxPackage.GetArchVersion(SoftwareVersion);
        var staging = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var source = staging / $"{packageName}-{archVersion}";
        var output = (AbsolutePath)$"{context.BundleOutputPath}.pkg.tar.zst";
        var temporaryOutput = CreateTemporaryPackageOutputPath(output);
        try
        {
            source.CreateDirectory();
            CreateLinuxPackageRoot(context, options, source, ApplicationPackagingType.LinuxArchPackage);
            var sourceArchive = staging / $"{source.Name}.tar.gz";
            ExecuteShell($"tar -czf {sourceArchive.ToString().QuoteShell()} -C {staging.ToString().QuoteShell()} " +
                         source.Name.QuoteShell(), staging);
            (staging / "PKGBUILD").WriteAllText(LinuxPackage.GetArchPkgBuild(packageName, archVersion,
                GetArchArchitecture(architecture), options.License, options.Summary, source.Name));
            ExecuteShell(CreateArchPackageBuildCommand(), staging);
            var package = Directory.GetFiles(staging, "*.pkg.tar.zst", SearchOption.TopDirectoryOnly)
                .SingleOrDefault();
            if (package is null)
                throw new FileNotFoundException("makepkg did not produce exactly one Arch Linux package.");
            ((AbsolutePath)package).Move(temporaryOutput, ExistsPolicy.FileOverwrite);
            MovePackageOutput(temporaryOutput, output, "Arch Linux");
        }
        finally
        {
            temporaryOutput.DeleteFile();
            DeleteFileSystemEntry(staging);
        }
    }

    /// <summary>Creates one Snap package using <c>snapcraft pack</c>.</summary>
    protected virtual void CreateLinuxSnap(PublishRidContext context, string architecture)
    {
        var options = LinuxAppBundleOptions;
        ResolveLinuxPathOptions(options);
        var packageName = LinuxPackage.GetSnapName(SoftwareName);
        var staging = PublishStagingDirectory / Guid.NewGuid().ToString("N");
        var payload = staging / "payload";
        var output = (AbsolutePath)$"{context.BundleOutputPath}.snap";
        var temporaryOutput = CreateTemporaryPackageOutputPath(output);
        try
        {
            payload.CreateDirectory();
            context.PublishPath.Copy(payload, ExistsPolicy.MergeAndOverwrite);
            UnixSystem.SetUnix755Executable(payload / options.ExecutableName!);
            PublishUtilities.WriteRuntimeManifest(payload, BuildRuntimeManifestFileName,
                new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true,
                    ApplicationPackagingType.LinuxSnap));
            var gui = payload / "meta" / "gui";
            gui.CreateDirectory();
            (gui / $"{packageName}.desktop").WriteAllText(
                LinuxAppBundle.GetDesktopEntry(options, packageName,
                    $"${{SNAP}}/meta/gui/icon{LinuxIconFile.Extension}"));
            ValidateLinuxIcon(LinuxIconFile);
            LinuxIconFile.Copy(gui / $"icon{LinuxIconFile.Extension}", ExistsPolicy.FileOverwrite);
            var snapDirectory = staging / "snap";
            snapDirectory.CreateDirectory();
            (snapDirectory / "snapcraft.yaml").WriteAllText(LinuxPackage.GetSnapcraftManifest(packageName,
                SoftwareVersion, GetSnapArchitecture(HostArchitecture), GetSnapArchitecture(architecture),
                options.ExecutableName!, options.Summary, options.Description, options.SnapBase,
                options.SnapConfinement, options.SnapPlugs));
            ExecuteShell(CreateSnapBuildCommand(), staging);
            var snap = Directory.GetFiles(staging, "*.snap", SearchOption.TopDirectoryOnly).SingleOrDefault()
                       ?? throw new FileNotFoundException("snapcraft did not produce exactly one Snap package.");
            ((AbsolutePath)snap).Move(temporaryOutput, ExistsPolicy.FileOverwrite);
            MovePackageOutput(temporaryOutput, output, "Snap");
        }
        finally
        {
            temporaryOutput.DeleteFile();
            DeleteFileSystemEntry(staging);
        }
    }

    private void CreateLinuxPackageRoot(PublishRidContext context, LinuxAppBundleOptions options, AbsolutePath root,
        ApplicationPackagingType packagingType)
    {
        var packageName = GetLinuxPackageName();
        var executable = options.ExecutableName!;
        var application = root / "usr" / "lib" / packageName;
        application.CreateDirectory();
        context.PublishPath.Copy(application, ExistsPolicy.MergeAndOverwrite);
        UnixSystem.SetUnix755Executable(application / executable);
        var bin = root / "usr" / "bin";
        bin.CreateDirectory();
        var launcher = bin / packageName;
        launcher.WriteAllText($"#!/bin/sh\nexec {$"/usr/lib/{packageName}/{executable}".QuoteShell()} \"$@\"\n");
        UnixSystem.SetUnix755Executable(launcher);
        var applications = root / "usr" / "share" / "applications";
        applications.CreateDirectory();
        (applications / $"{options.ApplicationId}.desktop").WriteAllText(
            LinuxAppBundle.GetDesktopEntry(options, packageName, options.IconName));
        var metainfo = root / "usr" / "share" / "metainfo";
        metainfo.CreateDirectory();
        (metainfo / $"{options.ApplicationId}.appdata.xml").WriteAllText(LinuxAppBundle.GetAppStreamMetadata(options));
        var icon = LinuxIconFile;
        ValidateLinuxIcon(icon);
        var icons = root / "usr" / "share" / "icons" / "hicolor" /
                    (icon.Extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ? "scalable" : "256x256") /
                    "apps";
        icons.CreateDirectory();
        icon.Copy(icons / $"{options.IconName}{icon.Extension}", ExistsPolicy.FileOverwrite);
        PublishUtilities.WriteRuntimeManifest(application, BuildRuntimeManifestFileName,
            new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true, packagingType));
    }

    /// <summary>Composes the Flatpak builder command.</summary>
    protected virtual string CreateFlatpakBuilderCommand(string architecture, AbsolutePath repository,
        AbsolutePath buildDirectory, AbsolutePath manifest)
    {
        return $"flatpak-builder --force-clean --disable-rofiles-fuse --arch={architecture.QuoteShell()} " +
               $"--repo={repository.ToString().QuoteShell()} {buildDirectory.ToString().QuoteShell()} " +
               manifest.ToString().QuoteShell();
    }

    /// <summary>Composes the Flatpak single-file bundle command.</summary>
    protected virtual string CreateFlatpakBundleCommand(string architecture, AbsolutePath repository,
        AbsolutePath output, string applicationId)
    {
        return $"flatpak build-bundle --arch={architecture.QuoteShell()} {repository.ToString().QuoteShell()} " +
               $"{output.ToString().QuoteShell()} {applicationId.QuoteShell()}";
    }

    /// <summary>Composes the RPM build command.</summary>
    protected virtual string CreateRpmBuildCommand(AbsolutePath topDirectory, AbsolutePath specFile)
    {
        return $"rpmbuild --define {$"_topdir {topDirectory}".QuoteShell()} -bb {specFile.ToString().QuoteShell()}";
    }

    /// <summary>Composes the Arch Linux binary-package build command.</summary>
    protected virtual string CreateArchPackageBuildCommand()
    {
        return "PACMAN=true PKGDEST=\"$PWD\" PKGEXT=.pkg.tar.zst makepkg --force --noconfirm --ignorearch";
    }

    private bool CanBuildFlatpakArchitecture(string architecture)
    {
        return HostArchitecture switch
        {
            Architecture.X64 => architecture is "x64",
            Architecture.Arm64 => architecture is "arm64",
            _ => false
        };
    }

    /// <summary>Composes the Snap build command.</summary>
    protected virtual string CreateSnapBuildCommand()
    {
        return "snapcraft pack --destructive-mode";
    }

    private string GetLinuxPackageName()
    {
        return LinuxPackage.GetPackageName(SoftwareName);
    }

    private static void MovePackageOutput(AbsolutePath temporaryOutput, AbsolutePath output, string packageType)
    {
        if (!temporaryOutput.FileExists())
            throw new FileNotFoundException($"{packageType} packaging did not produce '{temporaryOutput}'.",
                temporaryOutput);
        temporaryOutput.Move(output, ExistsPolicy.FileOverwrite);
    }

    private static AbsolutePath CreateTemporaryPackageOutputPath(AbsolutePath output)
    {
        return output.Parent / $".{output.Name}.{Guid.NewGuid():N}.tmp";
    }

    private static void SetDebianControlPermissions(AbsolutePath controlDirectory, AbsolutePath controlFile)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(controlDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        File.SetUnixFileMode(controlFile,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private static void ValidateLinuxIcon(AbsolutePath icon)
    {
        if (!icon.FileExists())
            throw new FileNotFoundException($"The configured Linux application icon '{icon}' does not exist.", icon);
        if (!icon.Extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) &&
            !icon.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The configured Linux application icon '{icon}' must use the .svg or .png extension.");
        }
    }

    private static string GetDebArchitecture(string architecture)
    {
        return architecture switch { "x64" => "amd64", "arm64" => "arm64", _ => architecture };
    }

    private static string GetRpmArchitecture(string architecture)
    {
        return architecture switch { "x64" => "x86_64", "arm64" => "aarch64", _ => architecture };
    }

    private static string GetArchArchitecture(string architecture)
    {
        return architecture switch { "x64" => "x86_64", "arm64" => "aarch64", _ => architecture };
    }

    private static string GetFlatpakArchitecture(string architecture)
    {
        return architecture switch { "x64" => "x86_64", "arm64" => "aarch64", _ => architecture };
    }

    private static string GetSnapArchitecture(string architecture)
    {
        return architecture switch { "x64" => "amd64", "arm64" => "arm64", _ => architecture };
    }

    private static string GetSnapArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => throw new InvalidOperationException(
                $"Host architecture '{architecture}' is not supported by Snapcraft.")
        };
    }

    /// <summary>
    /// Creates a unique temporary AppImage output path beside the final artifact.
    /// </summary>
    /// <param name="outputPath">The final AppImage output path.</param>
    /// <returns>A unique temporary path on the same filesystem.</returns>
    protected virtual AbsolutePath CreateTemporaryAppImageOutputPath(AbsolutePath outputPath)
    {
        return outputPath.Parent / $".{outputPath.Name}.{Guid.NewGuid():N}.tmp";
    }

    /// <summary>
    /// Creates the complete AppDir layout and metadata for one Linux runtime.
    /// </summary>
    /// <param name="context">The Linux runtime publish context.</param>
    /// <param name="appDirPath">The isolated AppDir staging path.</param>
    /// <exception cref="FileNotFoundException">
    /// The configured Linux icon or resolved published executable does not exist.
    /// </exception>
    protected virtual void CreateLinuxAppDir(PublishRidContext context, AbsolutePath appDirPath)
    {
        var linuxIconFile = LinuxIconFile;
        var iconExtension = linuxIconFile.Extension.ToLowerInvariant();
        if (iconExtension is not ".svg" and not ".png")
        {
            throw new InvalidOperationException(
                $"The configured Linux application icon '{linuxIconFile}' must use the .svg or .png extension.");
        }

        var options = LinuxAppBundleOptions;
        ResolveLinuxPathOptions(options);
        if (!linuxIconFile.FileExists())
        {
            throw new FileNotFoundException(
                $"The configured Linux application icon '{linuxIconFile}' does not exist.",
                linuxIconFile);
        }

        var publishedExecutable = context.PublishPath / options.ExecutableName!;
        if (!publishedExecutable.FileExists())
        {
            throw new FileNotFoundException(
                $"Published executable '{publishedExecutable}' does not exist for '{context.RuntimeIdentifier}'.",
                publishedExecutable);
        }

        var usrBinPath = appDirPath / "usr" / "bin";
        var applicationsPath = appDirPath / "usr" / "share" / "applications";
        var iconsPath = appDirPath / "usr" / "share" / "icons" / "hicolor" /
                        (iconExtension == ".svg" ? "scalable" : "256x256") / "apps";
        var metainfoPath = appDirPath / "usr" / "share" / "metainfo";
        usrBinPath.CreateDirectory();
        applicationsPath.CreateDirectory();
        iconsPath.CreateDirectory();
        metainfoPath.CreateDirectory();

        var appRunPath = appDirPath / "AppRun";
        var desktopFileName = $"{options.ApplicationId}.desktop";
        var desktopPath = appDirPath / desktopFileName;
        var installedDesktopPath = applicationsPath / desktopFileName;
        var iconFileName = $"{options.IconName}{iconExtension}";
        var appStreamPath = metainfoPath / $"{options.ApplicationId}.appdata.xml";
        var appRun = LinuxAppBundle.GetAppRunScript(options);
        var desktop = LinuxAppBundle.GetDesktopEntry(options);
        var appStream = LinuxAppBundle.GetAppStreamMetadata(options);

        appRunPath.WriteAllText(appRun);
        UnixSystem.SetUnix755Executable(appRunPath);
        desktopPath.WriteAllText(desktop);
        installedDesktopPath.WriteAllText(desktop);
        appStreamPath.WriteAllText(appStream);
        linuxIconFile.Copy(appDirPath / iconFileName, ExistsPolicy.FileOverwrite);
        linuxIconFile.Copy(iconsPath / iconFileName, ExistsPolicy.FileOverwrite);

        context.PublishPath.Copy(usrBinPath, ExistsPolicy.MergeAndOverwrite);
        UnixSystem.SetUnix755Executable(usrBinPath / options.ExecutableName!);
        PublishUtilities.WriteRuntimeManifest(usrBinPath, BuildRuntimeManifestFileName,
            new BuildRuntime(context.RuntimeIdentifier, SoftwareVersion, true,
                ApplicationPackagingType.LinuxAppImage));
    }

    private static void ResolveLinuxPathOptions(LinuxAppBundleOptions options)
    {
        options.ExecutableName = FileUtilities.ValidatePathLeafName(
            options.ExecutableName ?? options.ProductName,
            $"{nameof(LinuxAppBundleOptions)}.{nameof(LinuxAppBundleOptions.ExecutableName)}");
        options.IconName = FileUtilities.ValidatePathLeafName(
            options.IconName ?? options.ProductName,
            $"{nameof(LinuxAppBundleOptions)}.{nameof(LinuxAppBundleOptions.IconName)}");
        options.ApplicationId = FileUtilities.ValidatePathLeafName(
            options.ApplicationId,
            $"{nameof(LinuxAppBundleOptions)}.{nameof(LinuxAppBundleOptions.ApplicationId)}");
    }

    /// <summary>
    /// Downloads and extracts the appimagetool executable for the current host architecture.
    /// </summary>
    /// <returns>The cached extracted <c>AppRun</c> executable.</returns>
    protected virtual AbsolutePath PrepareAppImageTool()
    {
        var hostArchitecture = HostArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => throw new InvalidOperationException(
                $"Host architecture '{HostArchitecture}' is not supported by appimagetool.")
        };
        var cacheDirectory = AppImageToolCacheDirectory;
        var downloadedPath = cacheDirectory / $"appimagetool-{hostArchitecture}.AppImage";
        var extractedPath = cacheDirectory / $"squashfs-root-{hostArchitecture}";
        var extractedAppRun = extractedPath / "AppRun";

        cacheDirectory.CreateDirectory();
        if (extractedPath.DirectoryExists() && extractedAppRun.FileExists())
        {
            UnixSystem.SetUnix755Executable(extractedAppRun);
            return extractedAppRun;
        }

        DeleteFileSystemEntry(extractedPath);

        downloadedPath.DeleteDirectory();

        var downloadedNow = false;
        if (!downloadedPath.FileExists())
        {
            var url =
                $"{LinuxAppBundle.AppImageToolRepositoryUrl}/releases/download/continuous/appimagetool-{hostArchitecture}.AppImage";
            try
            {
                DownloadFile(url, downloadedPath);
                if (!downloadedPath.FileExists())
                    throw new FileNotFoundException($"Downloaded appimagetool '{downloadedPath}' does not exist.",
                        downloadedPath);

                UnixSystem.SetUnix755Executable(downloadedPath);
                downloadedNow = true;
            }
            catch
            {
                DeleteFileSystemEntry(downloadedPath);
                throw;
            }
        }

        if (!downloadedNow)
            UnixSystem.SetUnix755Executable(downloadedPath);

        if (!IsFuseAvailable)
            WarnFuseUnavailable();

        var extractionDirectory = cacheDirectory / $"extract-{Guid.NewGuid():N}";
        var extractionOutput = extractionDirectory / "squashfs-root";
        var extractionAppRun = extractionOutput / "AppRun";
        DeleteFileSystemEntry(extractionDirectory);
        extractionDirectory.CreateDirectory();
        try
        {
            ExecuteShell(CreateAppImageToolExtractionCommand(downloadedPath), extractionDirectory);
            if (!extractionOutput.DirectoryExists())
            {
                throw new DirectoryNotFoundException(
                    $"appimagetool extraction output '{extractionOutput}' does not exist.");
            }

            if (!extractionAppRun.FileExists())
            {
                throw new FileNotFoundException(
                    $"Extracted appimagetool executable '{extractionAppRun}' does not exist.",
                    extractionAppRun);
            }

            DeleteFileSystemEntry(extractedPath);
            extractionOutput.Move(extractedPath);
        }
        finally
        {
            DeleteFileSystemEntry(extractionDirectory);
        }

        UnixSystem.SetUnix755Executable(extractedAppRun);
        return extractedAppRun;
    }

    /// <summary>
    /// Composes the shell command that builds one AppImage.
    /// </summary>
    /// <param name="architecture">The target AppImage architecture.</param>
    /// <param name="appImageTool">The extracted appimagetool executable.</param>
    /// <param name="appDirPath">The staged AppDir path.</param>
    /// <param name="outputPath">The command output path.</param>
    /// <returns>The shell-safe AppImage build command.</returns>
    protected virtual string CreateAppImageBuildCommand(string architecture, AbsolutePath appImageTool,
        AbsolutePath appDirPath, AbsolutePath outputPath)
    {
        return $"ARCH={architecture} {appImageTool.ToString().QuoteShell()} " +
               $"{appDirPath.ToString().QuoteShell()} " +
               outputPath.ToString().QuoteShell();
    }

    /// <summary>
    /// Logs that AppImage bundles cannot be created on the current host.
    /// </summary>
    protected virtual void WarnLinuxAppImagesUnsupportedHost()
    {
        Log.Warning("Skipping Linux AppImage bundles on a non-Linux host.");
    }

    /// <summary>
    /// Logs that FUSE 2 is unavailable and extraction will be used instead.
    /// </summary>
    protected virtual void WarnFuseUnavailable()
    {
        Log.Warning("FUSE 2 is unavailable; appimagetool will be extracted before use.");
    }
}
