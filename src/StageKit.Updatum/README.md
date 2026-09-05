# StageKit.Updatum

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/main/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit.Updatum?style=for-the-badge)](https://www.nuget.org/packages/StageKit.Updatum)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

`StageKit.Updatum` is a lightweight, easy-to-integrate C# library that automates application updates through **GitHub Releases**.
It checks for new versions, retrieves release notes, discovers the right asset for the current runtime, downloads it with progress
reporting and optional verification, then prepares and runs a cross-platform update — installer, portable archive, single-file
executable, AppImage, Flatpak, Debian, RPM, Arch Linux, Snap, macOS app bundle, PKG, or DMG.

## Features

- **💻 Cross-platform:** Works on Windows, Linux, and macOS.
- **⚙️ Flexible integration:** Embeds into WPF, WinForms, Avalonia, or console applications.
- **🔍 Update checker:** Checks GitHub for the latest release manually or on a timer.
- **📦 Asset selection:** Picks the release asset for the current platform, architecture, and packaging type via regex.
- **📄 Changelog support:** Builds a formatted changelog from the releases ahead of the current version.
- **⬇️ Download with progress tracking:** Progress is exposed through `INotifyPropertyChanged` properties and events.
- **🔒 Download verification:** GitHub-native `sha256:` digest with `<asset>.sha256` sidecar fallback, plus a pluggable signature verifier.
- **🔄 Auto-upgrade:** Generates and runs platform scripts to replace portable and single-file installs, or defers to the system installer.
- **📦 Minimal dependencies:** Only `Octokit`; no external update infrastructure required.

## Install

```bash
dotnet add package StageKit.Updatum
```

Or add the package reference directly to your project:

```xml
<PackageReference Include="StageKit.Updatum" Version="*" />
```

The package targets .NET 8 and .NET 10.

## Requirements

1. Publish your application to GitHub Releases.
2. Name the assets so the platform and architecture are matchable, for example:
   - Windows: `MyApp_win-x64_v1.0.0.exe`, `MyApp_win-x64_v1.0.0.msi`, `MyApp_win-x64_v1.0.0.zip`
   - Linux: `MyApp_linux-x64_v1.0.0.AppImage`, `.flatpak`, `.deb`, `.rpm`, `.pkg.tar.zst`, `.snap`, or `.zip`
   - macOS: `MyApp_osx-arm64_v1.0.0.zip`, `.pkg`, or `.dmg`
   - See the [UVtools release assets](https://github.com/sn4k3/UVtools/releases/latest) for a real-world naming example.
   - Asset matching is configurable via regex (`AssetRegexPattern`) and an optional extension filter (`AssetExtensionFilter`).

The selected asset must be compatible with the application’s runtime identifier and packaging type. For example, a portable
Windows application might publish `MyApp_win-x64_v1.0.0.zip`, while an installed build might publish an `.msi` with the same
runtime identifier.

## Basic usage

```csharp
using StageKit.Updatum;

// Create one instance and keep it global. The current version defaults to the entry assembly version;
// pass it explicitly to be strict.
using var updater = new UpdatumManager("owner", "repository")
{
    AssetRegexPattern = "win-x64",
    AssetExtensionFilter = ".zip"
};

// Returns true when a newer release with a compatible asset is available.
if (!await updater.CheckForUpdatesAsync(cancellationToken)) return;

// Optionally show the changelog for every release ahead of the current version.
Console.WriteLine(updater.GetChangelog());

var download = await updater.DownloadUpdateAsync(cancellationToken);
if (download is null) return;

await updater.InstallUpdateAsync(
    download,
    forceTerminate: true,
    runArguments: null,
    cancellationToken);
```

`DownloadAndInstallUpdateAsync(...)` runs the download and install in one call and deletes the download if the install does not start.

Only one check, download, or install operation runs on a manager at a time. A competing operation returns `false` or `null`;
`State` and `IsBusy` stay active until the accepted operation completes. Cancellation and manager disposal restore the idle state.

## Asset selection

`AssetRegexPattern` filters release asset names and defaults to `EntryApplication.GenericRuntimeIdentifier`. When a release has
multiple matching assets and no `AssetExtensionFilter` is set, Updatum infers the best asset from the running `EntryApplication`
bundle type:

- Windows: `.exe` when running as a .NET single-file app, otherwise `.msi`
- Linux: `.AppImage`, `.flatpak`, `.deb`, `.rpm`, `.pkg.tar.zst`, or `.snap` for the matching runtime package type;
  otherwise `.zip`
- If nothing matches, the first matching asset is used

Set `AssetExtensionFilter` (for example `.zip`, `.msi`, `.AppImage`) to force a package type. You typically need a marker file in
the application folder to know whether the user runs the portable or the installer build.

Prereleases are excluded unless `AllowPreReleases` is enabled. Set `FetchOnlyLatestRelease` to request only GitHub's latest
release instead of scanning the configured release page (up to 30 releases, 1 page — tune with `GitHubApiOptions`).

## Auto-updater strategy

When you call the install path, Updatum:

- If the asset is a **zip** with a single entry, extracts it to a temporary folder and continues the other checks.
  - If the zip contains multiple files, it is treated as a **portable application**. A generated script performs checks, kills
    running instances, merges files, renames the version in the folder name, and starts the new instance.
  - On macOS, a zip update for an application bundle must contain exactly one top-level `.app`. Updatum stages and replaces
    that bundle itself, rather than treating its parent directory as the portable application directory.
- If the asset is a **single-file application** (.NET single-file executable or Linux AppImage), it is moved into the current
  folder and renamed to the current name and version.
- If the asset is an **installer**, it is executed and follows its normal installation process.

Supported targets:

- Portable applications (zip)
- .NET single-file publishes
- Windows installers (`.exe` and `.msi`)
- Linux [AppImage](https://appimage.org/)
- Linux [Flatpak](https://flatpak.org/)
- Linux Debian (`.deb`), RPM (`.rpm`), Arch Linux (`.pkg.tar.zst`), and Snap (`.snap`) packages
- macOS app bundle
- macOS PKG (`.pkg`) and disk image (`.dmg`) packages

### Installing updates

Portable archives and single-file applications use generated platform scripts. Replacement content is copied to a sibling
staging path first; the current installation is moved to a backup only after staging succeeds, and is restored when the final
swap fails.

`forceTerminate: true` lets generated scripts stop running application processes and terminates the current process after the
script starts. With `forceTerminate: false`, Updatum does not kill or exit the current application; the caller is responsible for
arranging a safe shutdown when replacing locked files.

Pass `UpdatumManager.NoRunAfterUpgradeToken` as `runArguments` to suppress relaunch. Flatpak updates first identify whether the
current application is installed in the user or system scope. When running inside Flatpak, Updatum stages the downloaded
bundle in the host-visible application cache and invokes the host Flatpak CLI through `flatpak-spawn --host`; the manifest
must grant `--talk-name=org.freedesktop.Flatpak`. System Flatpak updates and Debian, RPM, Arch Linux, and Snap packages request
elevation through `ProcessHelper`; Updatum waits for a zero exit code before reporting package-install completion,
relaunching, or terminating the current process. Denied elevation, timeout, cancellation, or installer failure stops that
continuation.

macOS app bundle, PKG, and DMG updates relaunch through Launch Services. The relaunch uses `open -n -W` to request a new
instance and confirm that it remains active after startup. PKG and DMG installation uses the native administrator prompt when privileges are actually required. PKG assets run
through `/usr/sbin/installer`, which always installs into the system domain and therefore always prompts. DMG assets are
mounted read-only and always detached; an embedded PKG is installed with `installer`, while an embedded app bundle is staged
and atomically replaced in its current location (or `/Applications` when no current bundle is known), with rollback on failure.
A DMG runs unprivileged first and only re-runs with the administrator prompt when it wraps a PKG or targets a directory the
current user cannot write. After a successful forced update, a non-elevated detached helper waits for the old process to exit
before reopening the installed application bundle, avoiding Launch Services activating the old instance instead. The helper
writes launch diagnostics to `StageKit.Updatum.Relaunch-<process-id>.log` in the system temporary directory.

Call `SafeDeleteFile()` on a downloaded asset when installation is not attempted. It removes the downloaded file and its empty
managed workspace on a best-effort basis.

For single-file updates, the default `EntryApplicationName` strategy preserves the current executable name and updates a
version embedded in that name when possible. Choose a custom or downloaded name when release assets use a different convention:

```csharp
updater.InstallUpdateSingleFileExecutableNameStrategy =
    UpdatumSingleFileExecutableNameStrategy.CustomName;
updater.InstallUpdateSingleFileExecutableName = "MyApp_v{0}";
```

`{0}` is replaced with the downloaded tag version. `CustomName` falls back to the current executable name and then the
downloaded asset name; `DownloadName` always keeps the downloaded asset name.

## Download verification

Downloads are placed in unique directories below the operating system temporary directory. Asset names containing path
components are rejected.

To require SHA-256 verification:

```csharp
updater.RequireAssetChecksum = true;
```

Updatum first retrieves the release asset's native `sha256:` digest through Octokit's authenticated GitHub API connection. The
metadata URL must match the configured GitHub API origin so credentials cannot be forwarded to an untrusted host. If GitHub does
not return a supported digest, Updatum falls back to a checksum release asset named exactly `<asset-name>.sha256` (suffix
configurable via `AssetChecksumSuffix`).

The fallback checksum file may contain a bare 64-character hexadecimal digest or the common `<digest>  <filename>` format. When
`RequireAssetChecksum` is enabled, the download fails if neither source is available. A verified download exposes `Sha256`.

Platform package trust is application-specific. Provide a verifier for Authenticode, Apple code signing, or another package policy:

```csharp
updater.RequireAssetSignatureVerification = true;
updater.AssetSignatureVerifier = async (filePath, cancellationToken) =>
{
    return await VerifyPackageTrustAsync(filePath, cancellationToken);
};
```

The download fails and its temporary workspace is removed if checksum or signature verification fails. Successful custom
verification is exposed through `IsSignatureVerified`.

## UI notifications

`EventSynchronizationContext` controls dispatch for update events and `INotifyPropertyChanged` notifications. The manager
captures `SynchronizationContext.Current` when constructed; set the property explicitly when construction happens away from the
UI thread (for Avalonia, `AvaloniaSynchronizationContext.Current`), or set it to `null` for direct invocation.

Bindable progress properties, all raising change notifications:

- `DownloadedMegabytes` / `DownloadSizeMegabytes` — text for a progress bar
- `DownloadedPercentage` — value for a progress bar (0–100)

Subscribe to `PropertyChanged` to redirect progress:

```csharp
updater.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(UpdatumManager.DownloadedPercentage))
    {
        Console.WriteLine($"Downloaded: {updater.DownloadedMegabytes} MB / {updater.DownloadSizeMegabytes} MB ({updater.DownloadedPercentage} %)");
    }
};
```

Adjust the progress notification frequency with `DownloadProgressUpdateFrequencySeconds` (default `0.1`; `0` reports every chunk).

For periodic checks, subscribe to `UpdateFound` and configure `AutoUpdateCheckTimer`. The timer uses the same manager and
does not start until `Start()` is called:

```csharp
updater.UpdateFound += (_, _) => Console.WriteLine("An update is available.");
updater.AutoUpdateCheckTimer.Interval = TimeSpan.FromHours(1).TotalMilliseconds;
updater.AutoUpdateCheckTimer.Start();
```

## FAQs

### Custom asset pattern

Your naming convention may differ from the default. Set `AssetRegexPattern` to a regex that matches your assets:

```csharp
// Expect assets named like: MyApp_winx64_v1.0.0
updater.AssetRegexPattern = RuntimeInformation.RuntimeIdentifier.Replace("-", string.Empty);
```

### Multiple assets with the same name but different extension

For `MyApp_win-x64_v1.0.0.zip` (portable) and `MyApp_win-x64_v1.0.0.msi` (installer), use `AssetExtensionFilter`. If omitted,
Updatum first applies its packaging-based extension preference and falls back to the first matching asset only when the
preferred extension is unavailable.

```csharp
if (IsPortableApp) updater.AssetExtensionFilter = "zip";
```

### Check for updates on a timer

Use the built-in `AutoUpdateCheckTimer` (default interval 12 hours) and listen for `UpdateFound`:

```csharp
updater.AutoUpdateCheckTimer.Interval = TimeSpan.FromHours(1).TotalMilliseconds;
updater.AutoUpdateCheckTimer.Start();
```

### Force an update

Pass an explicit base version to compare against the latest release. An empty version always triggers the update-found path:

```csharp
var updateFound = await updater.CheckForUpdatesAsync(new Version());
```

### My installer (.exe) or single-file app (.exe) does not upgrade

A Windows `.exe` can be an installer or a single-file application, and Updatum must pick the right strategy. By default it infers
from file signatures (Inno Setup, NSIS, Nullsoft, InstallShield, Windows Installer, …), which can produce false positives. Set
`InstallUpdateWindowsExeType` explicitly:

```csharp
using var updater = new UpdatumManager(owner, repository)
{
    // Auto           - infer from the asset file signature (use when assets contain both types)
    // Installer      - the .exe is an installer
    // SingleFileApp  - the .exe is a single-file executable
    InstallUpdateWindowsExeType = UpdatumWindowsExeType.Installer,

    // Arguments passed to msi/exe installers, e.g. "/qb" for a basic MSI UI
    InstallUpdateWindowsInstallerArguments = "/qb"
};
```

## Example

See `demo/StageKit.Demo` in the repository for a complete Avalonia example. Its `MainWindowViewModel` demonstrates the
full check, changelog, compatible-asset selection, checksum-verified download, cancellation, progress binding, manual
installation, automatic installation, and temporary-asset cleanup flow.

## License

StageKit.Updatum is licensed under the MIT License.
