# StageKit.Fallout

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/master/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

`StageKit.Fallout` is the reusable build-pipeline library behind StageKit's release process. It is a Fallout
(NUKE-style) build that discovers the solution and main project, resolves software metadata from MSBuild properties,
and exposes ready-made targets to restore, compile, run, and publish an application to every supported platform bundle.

This library is **not published to NuGet**. It is consumed through a `ProjectReference` from the repository's build
entry project (`builds/build/build.csproj`).

## Features

- Solution and main-project discovery, preferring `.slnx` over `.sln`
- Software metadata (name, company, RDNS, version, license, repository URL, tags) resolved from the main project's MSBuild properties
- Ready-made `Print`, `Clean`, `Restore`, `Compile`, `Run`, and `Publish` targets
- Self-contained + ReadyToRun publish for every configured runtime identifier
- Bundle creation: portable zip, .NET single-file, WiX installer, macOS `.app`, and Linux AppImage
- Multi-architecture macOS bundles (`osx-x64` + `osx-arm64` in one `.app`)
- Build runtime manifest emitted next to the published output for `StageKit.Runtime` packaging detection
- Release notes extracted from the top `CHANGELOG.md` section
- `virtual` members and callbacks throughout so a derived build can override any step

## Requirements

- .NET 10 SDK
- `Fallout.Common` and `Fallout.Utilities` 10.4.0
- The `fallout` global tool, pinned in `.config/dotnet-tools.json`

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
```

```bash
./build.sh Publish
```

## Targets

| Target | Depends on | Description |
|---|---|---|
| `Print` | — | Logs every public build variable, including resolved metadata and bundle options. Useful for diagnosing configuration. |
| `Clean` | runs before `Restore` | `dotnet clean` plus deletion of `ArtifactsDirectory`. |
| `Restore` | — | `dotnet restore` on `MainProject`. |
| `Compile` | `Restore` | `dotnet build` on `MainProject`. Default target. |
| `Run` | `Compile` | `dotnet run` on `MainProject` with `--no-build --no-restore`. |
| `Publish` | `Restore` | Publishes every runtime identifier in `RIds` and creates the bundles selected by `PublishBundles`. |

`DependOnTargets` lets a derived build inject extra targets into `Compile`, `Run`, and `Publish`.

## Parameters

Declared with Fallout's `[Parameter]` attribute, so each can be supplied on the command line or through the environment.

| Parameter | Default | Description |
|---|---|---|
| `Configuration` | `Release` | `Debug` or `Release`. |
| `RIds` | `win-x64 win-arm64 osx-x64 osx-arm64 linux-x64 linux-arm64` | Runtime identifiers to publish. |
| `PublishMultiArch` | `false` | Combine every architecture of a platform into one bundle. macOS requires both `osx-x64` and `osx-arm64`. |
| `PublishNoBundles` | `false` | Publish only; skip zip/app/installer creation. |
| `PublishDiscardNonBundles` | `false` | Delete the raw publish folders once bundles are created. |
| `PublishInstallerWithSingleFile` | `false` | Package the single-file executable inside the Windows installer instead of a separate normal publish. |

## Software metadata

Metadata is read from the main project's evaluated MSBuild properties, so the build never duplicates values that
already live in `Directory.Build.props` or the project file:

| Build property | MSBuild source |
|---|---|
| `ArtifactsDirectory` | `ArtifactsPath` |
| `SolutionName` | `SolutionName`, falling back to the solution name then `ProductName` |
| `SoftwareName` | `SolutionName` (override to use a different product name) |
| `SoftwareCompany` / `SoftwareCompanyRdns` | `Company` / `CompanyRDNS` |
| `SoftwareRDNS` | `<CompanyRDNS>.<SoftwareName>` |
| `SoftwareAuthors` | `Authors` |
| `SoftwareSummary` | `Summary`, falling back to `Description` |
| `SoftwareDescription` | `Description` |
| `SoftwareVersion` | `Version`, with a trailing `-dev` suffix removed |
| `SoftwareCopyright` | `Copyright` |
| `SoftwareLicense` | `PackageLicenseExpression` |
| `SoftwareRepositoryUrl` | `RepositoryUrl` |
| `SoftwarePackageTags` / `SoftwarePackageTagsList` | `PackageTags` |
| `BuildRuntimeManifestFileName` | `BuildRuntimeManifestFileName`, defaulting to `build-runtime.json` |

`MainProject` is the **last** runnable, non-excluded project in solution order. `ExcludedProjectNameTokens` disqualifies
candidates by name token (`test`, `demo`, `build`, `sample`, `fake`, `docs`, and more). Override `MainProject` directly
when the solution holds several candidates.

## Publishing

Each runtime identifier is published self-contained with ReadyToRun into `artifacts/publish/<AssetName>/`. The default
asset name is:

```
<SoftwareName>_<runtime-identifier>_v<SoftwareVersion>
```

Override it through the `AssetName` callback. Bundles are then created from that output:

| Bundle | `ApplicationPackagingType` flag | Host requirement | Output |
|---|---|---|---|
| Portable zip | `Portable` | Any | `<asset>.zip` (skipped for macOS RIDs when `MacOSAppBundle` is also enabled) |
| .NET single-file | `DotNetSingleFile` | Any | Single executable copied beside the publish folder |
| Windows installer | `WindowsInstaller` | Windows host **and** a WiX `.wixproj` in the solution | `.msi` / `.exe` produced by the installer project |
| macOS app bundle | `MacOSAppBundle` | Unix host | `<SoftwareName>.app` |
| Linux AppImage | `LinuxAppImage` | Linux host | `<asset>.AppImage` |

`PublishBundles` defaults to `Portable | DotNetSingleFile | WindowsInstaller | MacOSAppBundle | LinuxAppImage`. Bundles
whose host requirement is unmet are skipped with a warning rather than failing the build.

Icons are read from `MediaDirectory` (`media/` by default): `<SoftwareName>.icns` for macOS and `<SoftwareName>.svg`
for Linux. AppImage creation downloads and caches `appimagetool` for the host architecture; when FUSE 2 is unavailable
the tool is extracted before use.

`PublishCleanupExtensions` (default `wixpdb`) removes leftover files from the publish directory after a successful run.

### Build runtime manifest

Non-single-file publishes and portable zips receive a `BuildRuntime` JSON manifest (named by
`BuildRuntimeManifestFileName`) describing the runtime identifier, build version, packaging type, build timestamp, and
host OS description. `StageKit.Runtime` uses it to report the packaging shape at run time.

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

| Member | Purpose |
|---|---|
| `SoftwareName`, `SoftwareVersion`, `MainProject`, `Solution` | `virtual` — override when auto-detection is wrong |
| `ExcludedProjectNameTokens` | Mutable token list controlling `MainProject` detection |
| `BeforePublishRid` / `AfterPublishRid` | `Action<PublishRidContext>` hooks around each runtime publish |
| `AssetName` | `Func<PublishRidContext, string>` returning the base artifact name (simple file name, no directory or extension) |
| `ConfigurePublishRid` | `Func<DotNetPublishSettings, PublishRidContext, DotNetPublishSettings>` to adjust publish settings per RID |
| `CreateMacAppBundleOptions()` / `CreateLinuxAppBundleOptions()` | Lazily resolved bundle metadata (`Info.plist`, `.desktop`, AppStream, entitlements) |
| `PublishBundles`, `PublishCleanupExtensions`, `RIds` | Protected setters for build-wide publish configuration |
| `MediaDirectory`, `MacOSIconFile`, `LinuxIconFile`, `ChangelogFile`, `ReleaseNotesFile` | `virtual` path overrides |

Nearly every publish and bundle step (`CreatePublishSettings`, `PublishRuntime`, `CreateBundles`, `CreatePortableZip`,
`CreateMacOSApp`, `CreateLinuxAppImage`, `CreateWindowsInstallers`, …) is `protected virtual` and can be replaced.

## License

StageKit.Fallout is licensed under the MIT License.
