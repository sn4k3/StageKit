# StageKit.Updatum

`StageKit.Updatum` discovers GitHub releases, selects an asset for the current runtime, downloads it with progress reporting and optional verification, then prepares a cross-platform application update.

## Install

```bash
dotnet add package StageKit.Updatum
```

The package targets .NET 8 and .NET 10.

## Basic usage

```csharp
using StageKit.Updatum;

using var updater = new UpdatumManager("owner", "repository")
{
    AssetRegexPattern = "win-x64",
    AssetExtensionFilter = ".zip"
};

if (!await updater.CheckForUpdatesAsync(cancellationToken)) return;

var download = await updater.DownloadUpdateAsync(cancellationToken);
if (download is null) return;

await updater.InstallUpdateAsync(
    download,
    forceTerminate: true,
    runArguments: null,
    cancellationToken);
```

Only one check, download, or install operation can run on a manager at a time. A competing operation returns `false` or `null`; `State` and `IsBusy` remain active until the accepted operation completes. Cancellation and manager disposal restore the idle state.

## Asset selection

`AssetRegexPattern` filters release asset names. It defaults to `EntryApplication.GenericRuntimeIdentifier`. When a release has multiple matching assets, set `AssetExtensionFilter` to prefer a package type such as `.zip`, `.msi`, or `.AppImage`.

Prereleases are excluded unless `AllowPreReleases` is enabled. Set `FetchOnlyLatestRelease` to request only GitHub's latest release instead of scanning the configured release page.

## Download verification

Downloads are placed in unique directories below the operating system temporary directory. Asset names containing path components are rejected.

To require SHA-256 verification, publish a checksum release asset named exactly `<asset-name>.sha256` and enable:

```csharp
updater.RequireAssetChecksum = true;
```

The checksum file may contain a bare 64-character hexadecimal digest or the common `<digest>  <filename>` format. A verified download exposes `Sha256` and `IsChecksumVerified`.

Platform package trust is application-specific. Provide a verifier for Authenticode, Apple code signing, or another package policy:

```csharp
updater.RequireAssetSignatureVerification = true;
updater.AssetSignatureVerifier = async (filePath, cancellationToken) =>
{
    return await VerifyPackageTrustAsync(filePath, cancellationToken);
};
```

The download fails and its temporary workspace is removed if checksum or signature verification fails. Successful custom verification is exposed through `IsSignatureVerified`.

## Installing updates

Portable archives and single-file applications use generated platform scripts. Replacement content is copied to a sibling staging path first; the current installation is moved to a backup only after staging succeeds, and is restored when the final swap fails.

`forceTerminate: true` allows generated scripts to stop running application processes and terminates the current process after the script starts. With `forceTerminate: false`, Updatum does not kill or exit the current application; the caller is responsible for arranging a safe shutdown when replacing locked files.

Pass `UpdatumManager.NoRunAfterUpgradeToken` as `runArguments` to suppress relaunch. Flatpak installation observes cancellation, has a one-minute timeout, kills the child installer on cancellation or timeout, and follows both `forceTerminate` and no-relaunch settings.

Call `SafeDeleteFile()` on a downloaded asset when installation is not attempted. It removes the downloaded file and its empty managed workspace on a best-effort basis.

## UI notifications

`EventSynchronizationContext` controls dispatch for update events and `INotifyPropertyChanged` notifications. The manager captures `SynchronizationContext.Current` when constructed; set the property explicitly when construction happens away from the UI thread, or set it to `null` for direct invocation.
