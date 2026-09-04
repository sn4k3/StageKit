# StageKit.Fallout

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/main/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit.Fallout?style=for-the-badge)](https://www.nuget.org/packages/StageKit.Fallout)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

`StageKit.Fallout` is the reusable build-pipeline library behind StageKit's release process. It is a Fallout
(NUKE-style) build that discovers the solution and main project, resolves software metadata from MSBuild properties, and
exposes ready-made targets to restore, compile, run, and publish an application to every supported platform bundle.

## Features

- Solution and main-project discovery, preferring `.slnx` over `.sln`
- Software metadata (name, company, RDNS, version, license, repository URL, tags) resolved from the main project's
  MSBuild properties
- Ready-made `Print`, `Clean`, `Restore`, `Compile`, `Run`, `Publish`, and `GenerateInstallScript` targets
- Self-contained + ReadyToRun publish by default, with optional framework-dependent deployment
- Bundle creation: portable zip, .NET single-file, WiX installer, macOS `.app`/DMG/PKG, Linux AppImage, Flatpak, Debian,
  RPM, Arch Linux, and Snap packages
- Multi-architecture macOS bundles (`osx-x64` + `osx-arm64` in one `.app`)
- Versioned build runtime manifest emitted with each distributable for diagnostics and release tooling
- Release notes extracted from the top `CHANGELOG.md` section
- `virtual` members and callbacks throughout so a derived build can override any step

## Requirements

- .NET 10 SDK
- `Fallout.Common` and `Fallout.Utilities` 10.4.0
- The `fallout` global tool, pinned in `.config/dotnet-tools.json`

### Native packaging tools

The native Linux package tools are only required when the corresponding value is included in `PackagingTypes`:

| Packaging type     | Required tool                        | Debian/Ubuntu package                  | Fedora package                              | Arch package                |
|--------------------|--------------------------------------|----------------------------------------|---------------------------------------------|-----------------------------|
| `LinuxFlatpak`     | `flatpak`, `flatpak-builder`         | `flatpak flatpak-builder`              | `flatpak flatpak-builder`                   | `flatpak flatpak-builder`   |
| `LinuxSnap`        | `snapcraft`                          | Snap package (`snapd`), then Snapcraft | Snap package (`snapd`), then Snapcraft      | AUR `snapd`, then Snapcraft |
| `LinuxDeb`         | `dpkg-deb`                           | `dpkg-dev`                             | `dpkg`                                      | `dpkg`                      |
| `LinuxRpm`         | `rpmbuild`                           | `rpm`                                  | `rpm-build`                                 | `rpm`                       |
| `LinuxArchPackage` | `makepkg`, `pacman`, `tar`, `bsdtar`, `fakeroot`, `zstd` | `makepkg pacman-package-manager fakeroot zstd libarchive-tools` | Arch Linux host/container with `base-devel` | `base-devel`                |

Install the tools for a Debian or Ubuntu build host with:

```bash
sudo apt update
sudo apt install -y flatpak flatpak-builder dpkg-dev rpm makepkg pacman-package-manager fakeroot zstd libarchive-tools snapd
sudo snap install snapcraft --classic
```

Install the tools for a Fedora build host with:

```bash
sudo dnf install -y flatpak flatpak-builder dpkg rpm-build snapd
sudo systemctl enable --now snapd.socket
sudo snap install snapcraft --classic
```

Build Arch packages on Arch Linux (or an Arch Linux container) with:

```bash
sudo pacman -Syu --needed base-devel flatpak flatpak-builder dpkg rpm
```

For Snap builds on Arch, install `snapd` from the AUR, enable its socket, and then install Snapcraft:

```bash
yay -S snapd
sudo systemctl enable --now snapd.socket
sudo snap install snapcraft --classic
```

Flatpak also needs the runtime and SDK selected by `LinuxAppBundleOptions` (defaults: `org.freedesktop.Platform`,
`25.08`, and `org.freedesktop.Sdk`). Configure Flathub and install the defaults with:

```bash
flatpak remote-add --user --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak install --user -y flathub org.freedesktop.Platform//25.08 org.freedesktop.Sdk//25.08
```

Fallout invokes Snapcraft with `--destructive-mode`, which is suitable for an isolated CI runner and does not require
configuring an LXD or Multipass provider.

`LinuxAppImage` downloads and caches the matching `appimagetool` automatically. FUSE 2 is optional; when it is not
available, Fallout extracts the AppImage tool before running it. macOS DMG/PKG creation uses the platform tools
`hdiutil` and `pkgbuild`, and Windows Installer creation requires a WiX `.wixproj` and a Windows host.

## How it runs

```
build.ps1 / build.sh
  └─ bootstrap the .NET SDK if absent, then `dotnet tool restore`
      └─ dotnet fallout <target> [parameters]
          └─ compiles and runs builds/build/build.csproj  (configured by .fallout/parameters.json)
              └─ Build : StageKitBuild
```

`.fallout/parameters.json` points Fallout at the solution and the build project:

```json
{
  "$schema": "build.schema.json",
  "Solution": "StageKit.slnx",
  "BuildProjectFile": "builds/build/build.csproj"
}
```

Run a target:

```powershell
./build.ps1 Print
./build.ps1 Compile
./build.ps1 Publish
./build.ps1 GenerateInstallScript
```

```bash
./build.sh Publish
```

## Targets

| Target                  | Depends on            | Description                                                                                                            |
|-------------------------|-----------------------|------------------------------------------------------------------------------------------------------------------------|
| `Print`                 | —                     | Logs every public build variable, including resolved metadata and bundle options. Useful for diagnosing configuration. |
| `Clean`                 | runs before `Restore` | `dotnet clean` plus deletion of `ArtifactsDirectory`.                                                                  |
| `Restore`               | —                     | `dotnet restore` on `MainProject`.                                                                                     |
| `Compile`               | `Restore`             | `dotnet build` on `MainProject`. Default target.                                                                       |
| `Run`                   | `Compile`             | `dotnet run` on `MainProject` with `--no-build --no-restore`.                                                          |
| `Publish`               | `Restore`             | Publishes every runtime identifier in `RIds` and creates the packaging formats selected by `PackagingTypes`.           |
| `GenerateInstallScript` | —                     | Generates Bash `.sh` and Windows PowerShell `.ps1` installers for compatible GitHub release assets.                   |

`DependOnTargets` lets a derived build inject extra targets into `Compile`, `Run`, and `Publish`.

## Parameters

Declared with Fallout's `[Parameter]` attribute, so each can be supplied on the command line or through the environment.

| Parameter                   | Default                                                     | Description                                                                                      |
|-----------------------------|-------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| `Configuration`             | `Release`                                                   | `Debug` or `Release`.                                                                            |
| `PackagingTypes`            | `[Portable]`                                                | Unique packaging formats to create; use an empty array to skip packaging.                        |
| `RIds`                      | `win-x64 win-arm64 osx-x64 osx-arm64 linux-x64 linux-arm64` | Runtime identifiers to publish.                                                                  |
| `FrameworkDependent`        | `false`                                                     | Publish framework-dependent applications instead of self-contained applications.                 |
| `PublishMultiArch`          | `false`                                                     | Create one macOS app bundle containing both x64 and arm64 executables. Requires both macOS RIDs. |
| `DeletePublishDirectories`  | `false`                                                     | Delete raw publish directories after publishing.                                                 |
| `UseSingleFileForInstaller` | `false`                                                     | Use the single-file executable as the Windows installer payload.                                 |

## Software metadata

Metadata is read from the main project's evaluated MSBuild properties, so the build never duplicates values that already
live in `Directory.Build.props` or the project file:

| Build property                                    | MSBuild source                                                                                   |
|---------------------------------------------------|--------------------------------------------------------------------------------------------------|
| `ArtifactsDirectory`                              | `ArtifactsPath`                                                                                  |
| `SolutionName`                                    | Solution name, falling back through `Product`, `AssemblyName`, and `SolutionName`                |
| `SoftwareName`                                    | `SoftwareName`, then `RepositoryName`, then `SolutionName`; used for product and artifact naming |
| `SoftwareExecutableName`                          | `AssemblyName`, falling back to the main project name; used to locate the published executable   |
| `SoftwareCompany` / `SoftwareCompanyRdns`         | `Company` / `CompanyRDNS`                                                                        |
| `SoftwareRDNS`                                    | `<CompanyRDNS>.<SoftwareName>`                                                                   |
| `SoftwareAuthors`                                 | `Authors`                                                                                        |
| `SoftwareSummary`                                 | `Summary`, falling back to `Description`                                                         |
| `SoftwareDescription`                             | `Description`                                                                                    |
| `SoftwareVersion`                                 | `Version`, with a trailing `-dev` suffix removed                                                 |
| `SoftwareCopyright`                               | `Copyright`                                                                                      |
| `SoftwareLicense`                                 | `PackageLicenseExpression`                                                                       |
| `SoftwareRepositoryUrl`                           | `RepositoryUrl`                                                                                  |
| `SoftwarePackageTags` / `SoftwarePackageTagsList` | `PackageTags`                                                                                    |
| `BuildRuntimeManifestFileName`                    | `BuildRuntimeManifestFileName`, defaulting to `build-runtime.json`                               |

`MainProject` is the **last** runnable, non-excluded project in solution order. `ExcludedProjectNameTokens` disqualifies
candidates by name token (`test`, `demo`, `build`, `sample`, `fake`, `docs`, and more). Override `MainProject` directly
when the solution holds several candidates.

## Publishing

Each runtime identifier is published self-contained with ReadyToRun into `artifacts/publish/<AssetName>/` by default.
Set `FrameworkDependent` to `true` when the target system supplies the matching .NET runtime. The default asset name is:

```
<SoftwareName>_<runtime-identifier>_v<SoftwareVersion>
```

Override it through the `AssetName` callback. Bundles are then created from that output:

`SoftwareName` does not need to match the executable file name. Fallout resolves the executable stem from the main
project's evaluated `AssemblyName` through `SoftwareExecutableName`, adding `.exe` only for Windows RIDs. The default
macOS and Linux bundle launchers use the same executable name while retaining `SoftwareName` for display and artifact
names. Windows installer builds receive both values as `ApplicationName` and `ApplicationExecutableName`, so WiX
metadata, shortcuts, and install folders remain product-named while the payload points to the actual executable.

| Bundle            | `ApplicationPackagingType` value | Host requirement                                      | Output                                                                       |
|-------------------|----------------------------------|-------------------------------------------------------|------------------------------------------------------------------------------|
| Portable zip      | `Portable`                       | Any                                                   | `<asset>.zip` (skipped for macOS RIDs when `MacOSAppBundle` is also enabled) |
| .NET single-file  | `DotNetSingleFile`               | Any                                                   | Single executable copied beside the publish folder                           |
| Windows installer | `WindowsInstaller`               | Windows host **and** a WiX `.wixproj` in the solution | `.msi` / `.exe` produced by the installer project                            |
| macOS app bundle  | `MacOSAppBundle`                 | Unix host                                             | `<asset>.app`                                                         |
| macOS disk image  | `MacOSDmg`                       | macOS host with `hdiutil`                             | `<asset>.dmg`                                                                |
| macOS installer   | `MacOSPkg`                       | macOS host with `pkgbuild`                            | `<asset>.pkg`                                                                |
| Linux AppImage    | `LinuxAppImage`                  | Linux host                                            | `<asset>.AppImage`                                                           |
| Linux Flatpak     | `LinuxFlatpak`                   | Linux host with `flatpak-builder`                     | `<asset>.flatpak`                                                            |
| Linux Snap        | `LinuxSnap`                      | Linux host with `snapcraft`                           | `<asset>.snap`                                                               |
| Debian Deb        | `LinuxDeb`                       | Linux host with `dpkg-deb`                            | `<asset>.deb`                                                                |
| RHEL RPM          | `LinuxRpm`                       | Linux host with `rpmbuild`                            | `<asset>.rpm`                                                                |
| Arch package      | `LinuxArchPackage`               | Linux host with `makepkg`                             | `<asset>.pkg.tar.zst`                                                        |

`PackagingTypes` defaults to `[Portable]` in `StageKitBuild`. Derived builds select multiple formats with an array:

```csharp
PackagingTypes =
[
    ApplicationPackagingType.Portable,
    ApplicationPackagingType.LinuxArchPackage
];
```

Duplicate values and `None` are removed while preserving selection order. Formats whose host requirement is unmet are
skipped with a warning rather than failing the build. Set `PackagingTypes` to `[]` to publish runtime outputs without
creating packages.

### Installation script

Run `GenerateInstallScript` to create `scripts/install-<software-name>.sh` and
`scripts/install-<software-name>.ps1`. The target is independent of `Publish`, so it can describe packages produced by
separate Windows, Linux, and macOS runners without trying to build them locally. A script is omitted when none of its
supported formats are selected. Every run prints a command header, its help command shows detailed usage, `list` shows
all published GitHub release versions, and `list-changelog` shows GitHub release notes for up to 20 versions by default.
Pass a different positive limit when needed. Install the latest version or select an older release to downgrade:

```bash
./scripts/install-myapp.sh
./scripts/install-myapp.sh install v1.2.3
./scripts/install-myapp.sh --version 1.2.3
./scripts/install-myapp.sh --list
./scripts/install-myapp.sh --list-changelog
./scripts/install-myapp.sh --list-changelog 5
./scripts/install-myapp.sh help
```

```powershell
.\scripts\install-myapp.ps1
.\scripts\install-myapp.ps1 install v1.2.3
.\scripts\install-myapp.ps1 -Version 1.2.3
.\scripts\install-myapp.ps1 -List
.\scripts\install-myapp.ps1 -ListChangelog
.\scripts\install-myapp.ps1 -ListChangelog -ChangelogLimit 5
.\scripts\install-myapp.ps1 -Help
```

The Bash `list-changelog` command uses `jq` when available and otherwise falls back to `python3` to decode GitHub's
release-note JSON.

Only formats present in `PackagingTypes` are emitted. Their priority follows the insertion order of
`ApplicationPackagingInfo.KnownPackagingTypes`. Linux first tries the package native to the detected distribution
(DEB, RPM, or Arch), followed by AppImage, Flatpak, Snap, .NET single-file, and finally Portable. macOS tries the
app-bundle archive, followed by PKG and DMG. Incompatible formats are skipped and the next selected format is tried. The
release asset must contain Fallout's runtime identifier (`linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, or
`osx-multiarch`) and use the standard extension for its packaging type. This is automatic with the default `AssetName`
callback.

Windows PowerShell first tries a Windows installer (`.msi`, then `.exe`), followed by the .NET single-file executable
and finally the Portable zip. Single-file and Portable packages install under `%LOCALAPPDATA%\Programs\<package>` and
add the executable directory to the current user's `PATH`. MSI packages run passively and request elevation when
needed; whether an MSI permits installing an older version is controlled by that installer project. Missing release
versions and GitHub request failures are reported as concise script errors instead of raw PowerShell exceptions.

Explicit version selection accepts tags with or without the leading `v` and enables package-manager downgrade options
for DEB and RPM installation. Arch, Flatpak, Snap, portable, and application-bundle formats replace or update the
currently installed package with the selected release according to their native tool behavior.

The repository metadata must identify GitHub through an HTTPS URL such as `https://github.com/owner/repository` or an
SSH URL such as `git@github.com:owner/repository.git`. Override `InstallScriptFile`, `WindowsInstallScriptFile`,
`CreateInstallScript()`, `CreateWindowsInstallScript()`, or `ExecuteGenerateInstallScript()` in a derived build to
customize the output paths, contents, or write step.

DMG and PKG creation stage and sign the same `.app` layout used by `MacOSAppBundle`. DMG output includes an
`Applications` symlink for drag-and-drop installation, while PKG output installs the app directly in `/Applications`.
With `PublishMultiArch`, both native formats contain the combined `osx-x64` and `osx-arm64` app.
Avalonia applications should also set `Application.Name` in `App.axaml` to the same value as `SoftwareName`; otherwise,
Avalonia may replace the macOS menu title at runtime with its `Avalonia Application` fallback despite the generated
`CFBundleName` and `CFBundleDisplayName` values.

Debian, RPM, and Arch Linux payloads install the application under `/usr/lib/<package>` and a launcher under
`/usr/bin/<package>`. When selecting `LinuxDeb`, set
`LinuxAppBundleOptions.DebPackageMaintainer` to `Full Name <email@example.com>`. Snap defaults to the `core24` base,
strict confinement, common desktop interfaces, and the `libicu74` stage package required by self-contained .NET apps;
customize `SnapBase`, `SnapConfinement`, `SnapPlugs`, or `SnapStagePackages` through `LinuxAppBundleOptions`. Stage-package
names are base-specific, so update `SnapStagePackages` when changing `SnapBase`. Native package tools and the selected
Flatpak/Snap runtime bases must already be available on
the build host. Debian payloads are staged in the operating system's temporary directory so `dpkg-deb` receives valid
Unix permissions even when the repository is on a Windows-mounted WSL path such as `/mnt/c` or `/mnt/d`.

Set `LinuxAppBundleOptions.FlatpakAllowHostCommandExecution` to `true` when a Flatpak application must use
`flatpak-spawn --host`. This adds the broad `--talk-name=org.freedesktop.Flatpak` sandbox permission and is disabled by
default; prefer a narrower portal operation whenever one is available. StageKit.Updatum uses this host bridge
automatically for Flatpak package installation and stages its downloaded bundle in the host-visible application cache.

Icons are read from `MediaDirectory` (`media/` by default): `<SoftwareName>.icns` for macOS and `<SoftwareName>.svg`
for Linux. Override `LinuxIconFile` to use a `.png` icon; both SVG and PNG are accepted. SVG icons are installed in the
scalable hicolor directory, while PNG icons are installed in `256x256/apps`. AppImage creation downloads and caches
`appimagetool` for the host architecture; when FUSE 2 is unavailable the tool is extracted before use.

`PublishCleanupExtensions` (default `wixpdb`) removes leftover files from the publish directory after a successful run.

### Build runtime manifest

Each distributable receives a `BuildRuntime` JSON manifest named by `BuildRuntimeManifestFileName`. Portable archives,
installer payloads, macOS bundles, and AppImages stage it beside the application payload; single-file publishing embeds
it as bundled content. `StageKit.Runtime` automatically exposes the default `build-runtime.json` file through
`BuildRuntime.Instance`; use `BuildRuntime.TryLoad(...)` when this Fallout file name is customized.

The schema is versioned independently through `SchemaVersion` so consumers can reject or migrate future layouts:

```json
{
  "SchemaVersion": 1,
  "Runtime": "linux-x64",
  "IsBundle": true,
  "PackagingType": "LinuxAppImage",
  "BuildDateTimeUtc": "2026-08-28T12:34:56.789Z",
  "BuildOSDescription": "Ubuntu 24.04 LTS",
  "BuildVersion": "1.0.0"
}
```

### Release notes

`Publish` extracts the top section of `ChangelogFile` (`CHANGELOG.md`) into `ReleaseNotesFile` (`RELEASE_NOTES.md`)
before publishing, ready for a release job to consume.

## Customizing the build

Derive from `StageKitBuild` in the build entry project:

```csharp
using Serilog;
using StageKit.Fallout;

internal class Build : StageKitBuild
{
    public Build()
    {
        BeforePublishRid = context =>
            Log.Information("Publishing {Rid} to {Path}", context.RuntimeIdentifier, context.PublishPath);

        // Allow a demo project to be detected as the MainProject.
        ExcludedProjectNameTokens.Remove("demo");
    }

    public override string SoftwareName => MainProject.Name;

    protected override LinuxAppBundleOptions CreateLinuxAppBundleOptions()
    {
        var options = base.CreateLinuxAppBundleOptions();
        options.Categories = ["Development"];
        options.AppRunScriptBeforeExec = "echo 'Starting...'";
        return options;
    }

    public new static int Main() => Execute<Build>(x => x.Compile);
}
```

### Extension points

| Member                                                                                  | Purpose                                                                                                          |
|-----------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| `SoftwareName`, `SoftwareExecutableName`, `SoftwareVersion`, `MainProject`, `Solution`  | `virtual` — override when auto-detection is wrong                                                                |
| `ExcludedProjectNameTokens`                                                             | Mutable token list controlling `MainProject` detection                                                           |
| `BeforePublishRid` / `AfterPublishRid`                                                  | `Action<PublishRidContext>` hooks around each runtime publish                                                    |
| `AssetName`                                                                             | `Func<PublishRidContext, string>` returning the base artifact name (simple file name, no directory or extension) |
| `ConfigurePublishRid`                                                                   | `Func<DotNetPublishSettings, PublishRidContext, DotNetPublishSettings>` to adjust publish settings per RID       |
| `CreateMacAppBundleOptions()` / `CreateLinuxAppBundleOptions()`                         | Lazily resolved bundle metadata (`Info.plist`, `.desktop`, AppStream, entitlements)                              |
| `ConfigureWindowsInstallerBuildSettings(...)`                                           | Adjusts the MSBuild settings passed to each WiX installer project                                                |
| `PackagingTypes`, `PublishCleanupExtensions`, `RIds`                                    | Protected setters for build-wide publish configuration                                                           |
| `MediaDirectory`, `MacOSIconFile`, `LinuxIconFile`, `ChangelogFile`, `ReleaseNotesFile` | `virtual` path overrides                                                                                         |

Nearly every publish and bundle step (`CreatePublishSettings`, `PublishRuntime`, `CreateBundles`, `CreatePortableZip`,
`CreateMacOSApp`, `CreateLinuxAppImage`, `CreateWindowsInstallers`, …) is `protected virtual` and can be replaced.

## License

StageKit.Fallout is licensed under the MIT License.
