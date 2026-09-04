# StageKit

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/main/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit?style=for-the-badge)](https://www.nuget.org/packages/StageKit)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit is a lightweight .NET application infrastructure library for JSON settings files, observable settings objects,
crash report capture, application runtime metadata, and unhandled exception handling.

The repository also includes smaller packages for reusable building blocks:
[`StageKit.Primitives`](src/StageKit.Primitives/README.md) for low-level primitives,
[`StageKit.Runtime`](src/StageKit.Runtime/README.md) for entry-application/runtime inspection helpers, and
[`StageKit.Updatum`](src/StageKit.Updatum/README.md) for GitHub release updates.
[`StageKit.Fallout`](src/StageKit.Fallout/README.md) is the build-time pipeline library that publishes and bundles the
applications those packages ship in.

## Features

- Singleton JSON settings files with lazy load, manual save, AutoSave, and debounced save support
- Settings schema versioning with migration and validation/repair hooks
- AutoSave suspension and batch update scopes
- Observable settings base classes powered by `CommunityToolkit.Mvvm`
- Collection-backed settings files using `ObservableList<T>` with `ItemsView` for synchronized binding
- Save hooks through `BeforeSave()` and `AfterSave()`
- Pending debounce tracking with timeout-aware wait support
- Single-instance process guard based on a named mutex
- Atomic file writes, profile backup/restore, support bundle export, and retention helpers
- Dependency-light primitives package for atomic file writes, host URL/file-manager launching, and disposable/resource helpers
- First-run and onboarding state persistence
- Serializable crash reports with exception chains, stack traces, runtime information, and process stats
- AppDomain and task scheduler unhandled exception helpers
- Panic-save support for registered `ISavable` settings before forced process exit
- Configurable profile, config, and log paths
- Portable profile path parsing with `ApplicationKit.IsPortable` state
- Small application "birthday" helpers for version/about screens
- GitHub release discovery, secure asset downloads, and staged cross-platform application updates
- Reusable build pipeline that publishes and bundles applications for Windows, Linux, and macOS, and generates Bash and
  Windows PowerShell GitHub Releases installation scripts from the selected package formats

## Repository Layout

The repository keeps publishable libraries, tests, demos, and build tooling in separate top-level folders:

| Folder     | Purpose                                                                  |
|------------|--------------------------------------------------------------------------|
| `src/`     | Publishable StageKit libraries and the Fallout build-pipeline library    |
| `tests/`   | xUnit v3 test projects for the libraries                                 |
| `demo/`    | Small runnable demonstration applications                                |
| `builds/`  | Fallout build entry point and WiX installer project                      |
| `.github/` | Continuous integration, security scanning, and package release workflows |

`StageKit.slnx` remains at the repository root and references projects from these folders. Generated output belongs in
ignored directories such as `artifacts/` and `TestResults/`.

## Install

```bash
dotnet add package StageKit
```

For only the low-level primitives:

```bash
dotnet add package StageKit.Primitives
```

For only runtime and entry-application helpers:

```bash
dotnet add package StageKit.Runtime
```

For GitHub release discovery and application updates:

```bash
dotnet add package StageKit.Updatum
```

## Requirements

- .NET 8 or newer
- C# latest language version

## Packages

| Package               | NuGet                                                                                                                                | Docs                                        | Description                                                                                                                                                                             |
|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `StageKit`            | [![Nuget](https://img.shields.io/nuget/v/StageKit?style=flat-square)](https://www.nuget.org/packages/StageKit)                       | This file                                   | Application infrastructure: settings, crash reports, retention, backups, support bundles, single-instance guards, and app metadata.                                                     |
| `StageKit.Primitives` | [![Nuget](https://img.shields.io/nuget/v/StageKit.Primitives?style=flat-square)](https://www.nuget.org/packages/StageKit.Primitives) | [README](src/StageKit.Primitives/README.md) | Dependency-light primitives: atomic file writes, path and shell helpers, temporary resources, disposable base types, GC handles, and unmanaged memory wrappers. |
| `StageKit.Runtime`    | [![Nuget](https://img.shields.io/nuget/v/StageKit.Runtime?style=flat-square)](https://www.nuget.org/packages/StageKit.Runtime)       | [README](src/StageKit.Runtime/README.md)    | Entry-application and runtime helpers: assembly metadata, process paths, build manifests, bundle detection, relaunch utilities, and combined diagnostics through `RuntimeDiagnostics`.  |
| `StageKit.Updatum`    | [![Nuget](https://img.shields.io/nuget/v/StageKit.Updatum?style=flat-square)](https://www.nuget.org/packages/StageKit.Updatum)       | [README](src/StageKit.Updatum/README.md)    | GitHub release discovery, optional SHA-256 and platform-signature verification, download progress, and staged Windows/Linux/macOS update installation.                                  |
| `StageKit.Fallout`    | [![Nuget](https://img.shields.io/nuget/v/StageKit.Fallout?style=flat-square)](https://www.nuget.org/packages/StageKit.Fallout)       | [README](src/StageKit.Fallout/README.md)    | Build-time only: reusable Fallout build pipeline with restore/compile/run/publish targets and portable, single-file, WiX installer, macOS `.app`, and Linux AppImage bundling.          |

## Application Updates

Use `StageKit.Updatum` to select a compatible GitHub release asset, download it into an isolated temporary workspace,
and start a staged update:

```csharp
using StageKit.Updatum;

using var updater = new UpdatumManager("owner", "repository")
{
    AssetRegexPattern = "win-x64",
    AssetExtensionFilter = ".zip",
    RequireAssetChecksum = true
};

if (await updater.CheckForUpdatesAsync(cancellationToken))
{
    var download = await updater.DownloadUpdateAsync(cancellationToken);
    if (download is not null)
    {
        await updater.InstallUpdateAsync(download, true, null, cancellationToken);
    }
}
```

Checksum verification uses GitHub's native `sha256:` release-asset digest when available. For assets without native
digest metadata, publish a fallback sidecar named exactly `<asset-name>.sha256`. Configure `AssetSignatureVerifier` when
the application must also enforce a platform-specific trust policy such as Authenticode or macOS code signing. See the
`StageKit.Updatum` package README for installation behavior and security details.

## Quick Start

Configure StageKit during application startup:

```csharp
using StageKit;

ApplicationKit.ApplicationName = "MyApp";
ApplicationKit.ApplicationArgs = args;
ApplicationKit.Logger = logger;
ApplicationKit.UiFrameworkInfo = $"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version!.ToString(3)}";
ApplicationKit.Birth = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

UnhandledExceptions.UnobservedTaskExceptionIsTerminating = false;
UnhandledExceptions.RegisterAppDomainUnhandledException();
UnhandledExceptions.RegisterTaskSchedulerUnobservedTaskException();

CrashReportsFile.IsEnabled = true;
```

## Settings Files

Create a root settings file by inheriting from `RootSettingsFile<T>`:

```csharp
using StageKit;

public partial class AppSettings : RootSettingsFile<AppSettings>
{
    [ObservableProperty]
    public partial string Theme { get; set; } = "System";

    [ObservableProperty]
    public partial string ThemeColor { get; set; } = "Blue";

    [ObservableProperty]
    public partial bool EnableCrashReporting { get; set; } = true;

    [ObservableProperty]
    public partial long LastRunTimestamp { get; set; }

    public AppSettings()
    {
        FileName = "appsettings.json";
        AutoSave = true;
    }
}
```

Enable `AutoSave` when property changes should save automatically:

```csharp
var settings = AppSettings.Instance;
settings.AutoSave = true;
settings.Theme = "Dark";
```

`AutoSave` defaults to `false`. It starts reacting only after the settings object is loaded, so property changes caused
by JSON hydration or `OnLoaded(...)` do not rewrite the file during startup.

Keep `AutoSave` disabled when you prefer manual control:

```csharp
AppSettings.Instance.AutoSave = false;

AppSettings.SaveInstance();
```

Use debounced save APIs when you want to batch rapid changes:

```csharp
settings.DebouncedSave();

var saved = await settings.WaitForDebouncedSaveAsync(
    TimeSpan.FromSeconds(5),
    cancellationToken);

if (!saved)
{
    // timeout elapsed while the save was still pending
}
```

`Save()` cancels any pending debounced save before writing. `SaveCount` tracks successful saves for the current
in-memory instance and is ignored in JSON. `CancelDebouncedSave()` cancels a scheduled save without writing.

Suspend AutoSave while applying several related changes:

```csharp
settings.BatchUpdate(() =>
{
    settings.Theme = "Dark";
    settings.ThemeColor = "Violet";
});

using (settings.SuspendAutoSave(saveOnDispose: false))
{
    settings.Theme = "Light";
}
```

`BatchUpdate(...)` and `SuspendAutoSave(...)` still mark the file dirty while AutoSave is suspended. When the outermost
scope exits, StageKit schedules one debounced save if changes happened and saving on dispose is enabled.

By default, settings are stored under:

```csharp
ApplicationKit.ProfilePath
```

Override `DirectoryPath` or set `ApplicationKit.ProfilePath` to customize storage.

Parse startup arguments to support custom and portable profile paths:

```csharp
ApplicationKit.ApplicationArgs = args;
ApplicationKit.ParseProfilePathFromArgs();

if (ApplicationKit.IsPortable)
{
    // ProfilePath was selected from the portable profile location.
}
```

Launch a new or helper instance while keeping startup arguments such as profile or portable mode:

```csharp
ApplicationKit.LaunchNewInstanceKeepApplicationArgs("--open-settings");
```

The preserved arguments skip the executable path and any existing crash-report argument pair.

Use schema versioning and validation hooks for app-owned settings evolution:

```csharp
public sealed class AppSettings : RootSettingsFile<AppSettings>
{
    protected override int CurrentSettingsVersion => 2;

    public string Theme { get; set; } = "System";

    protected override void MigrateSettings(SettingsMigrationContext context)
    {
        if (context.FromVersion < 2)
        {
            Theme = "System";
        }
    }

    protected override void ValidateSettings(SettingsValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(Theme)) return;

        Theme = "System";
        context.MarkChanged("Theme was empty.");
    }
}
```

`SettingsVersion` is serialized with each settings file. Older files are migrated to `CurrentSettingsVersion` and kept
dirty so the upgraded schema can be persisted. If a file was written by a newer app version, StageKit renames it to
`<file>.unsupported-version-<timestampUtc>` and falls back to a fresh instance.

If a settings file fails to deserialize on load (corrupt JSON), StageKit renames it to `<file>.corrupt-<timestampUtc>`
and falls back to a fresh instance. Original data is preserved on disk for inspection or recovery.

## Collection Settings

Use `RootCollectionFile<T, TO>` when a settings file is mainly a list:

```csharp
using StageKit;

public sealed class RecentFiles : RootCollectionFile<RecentFiles, string>
{
    public RecentFiles()
    {
        FileName = "recent-files.json";
        TrimCollectionWhenExceeding = 20;
        TrimCollectionSide = CollectionSide.Head;
    }
}
```

```csharp
RecentFiles.Instance.Add(@"C:\work\project.txt");
RecentFiles.SaveInstance();
```

`RootCollectionFile<T, TO>` exposes `Items` as an `ObservableList<TO>` and `ItemsView` as a synchronized view for UI
binding. Set `TrackItemsWithChangeNotification = true` in the constructor when collection items implement
`INotifyPropertyChanged` and item property changes should mark the file dirty and trigger `AutoSave`; keep it disabled
for immutable items or very large collections.

## Observable Objects

`SubSettings` is based on `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`:

```csharp
using StageKit;

public sealed class ViewState : SubSettings
{
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
```

Classes deriving from `SubSettings`, `RootSettingsFile<T>`, or `RootCollectionFile<T, TO>` can use CommunityToolkit's
`[ObservableProperty]` generator:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using StageKit;

public sealed partial class AppSettings : RootSettingsFile<AppSettings>
{
    public AppSettings()
    {
        FileName = "appsettings.json";
    }

    [ObservableProperty]
    private string _theme = "System";
}
```

StageKit references `CommunityToolkit.Mvvm`, but if your app uses generator attributes such as `[ObservableProperty]`,
reference `CommunityToolkit.Mvvm` directly in that app too so the analyzer/source generator runs in the consuming
project.

## Crash Reports

Create crash reports directly:

```csharp
try
{
    RunApplication();
}
catch (Exception ex)
{
    var report = new CrashReport(ex, "Startup");
    Console.WriteLine(report.FormattedMessage);
}
```

StageKit can also capture unhandled exceptions:

```csharp
UnhandledExceptions.HandleCrashReport = report =>
{
    Console.WriteLine(report.FormattedMessage);
    return true;
};
```

If `CrashReportsFile.IsEnabled` is `true`, fatal unhandled exceptions are added to `CrashReportsFile.Instance`.

Unobserved task exceptions are marked observed and logged without terminating by default. Set
`UnhandledExceptions.UnobservedTaskExceptionIsTerminating` to `true` during startup when they should use the fatal
handling path.

Register settings that should be saved before StageKit forces process exit after a fatal exception:

```csharp
UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(AppSettings.Instance);
```

`SettingsFilesToSaveBeforeCrash` is a `HashSet<StageKit.Interfaces.ISavable>`, so any type implementing `ISavable` can
participate.

## Single Instance Guard

Use `ApplicationInstanceGuard` when an app should allow only one primary process:

```csharp
using var guard = ApplicationInstanceGuard.Acquire("MyCompany.MyApp");

if (guard.IsSecondary)
{
    return;
}

RunApplication();
```

The guard uses a named mutex, but disposal only closes its handle and may run on any thread. A still-owned mutex becomes
abandoned, and the next `Acquire(...)` treats that state as primary. The guard does not forward activation arguments yet.

If your app also launches a crash-report viewer with `ApplicationKit.CrashReportFlag`, check the crash-report mode
before blocking secondary instances, or use a different instance name for the viewer process.

## Storage Utilities

Use `SafeFile` from `StageKit.Primitives` when application code needs an atomic write outside `RootSettingsFile<T>`:

```csharp
using StageKit.Primitives;

SafeFile.WriteAllText(path, json);
SafeFile.Write(path, stream => JsonSerializer.Serialize(stream, model));
```

Use `SafeFileStream` when stream-style code should still write atomically:

```csharp
using var stream = new SafeFileStream(path);
JsonSerializer.Serialize(stream, model);
// Dispose commits by default.
```

Set `commitOnDispose: false` when the caller should explicitly choose whether to replace the destination:

```csharp
await using var stream = new SafeFileStream(path, commitOnDispose: false);
await JsonSerializer.SerializeAsync(stream, model, cancellationToken: cancellationToken);
await stream.CommitAsync(cancellationToken);
```

Use the IO helpers for path checks and temporary workspace cleanup:

```csharp
if (!PathUtilities.IsSubPathOf(candidatePath, rootPath))
{
    throw new InvalidOperationException("Path escapes the root directory.");
}

using var directory = new TemporaryDirectory(prefix: "stagekit");
using var file = new TemporaryFile(extension: "json");
```

Launch external tools with optional administrator elevation through Windows `runas`, Linux `pkexec`, or macOS
`osascript`. Already-privileged processes skip the elevation wrapper:

```csharp
using StageKit.Primitives.System;

if (!HostSystem.TryFindExecutable("system-tool", out string? toolPath))
    throw new FileNotFoundException("system-tool was not found.");

int exitCode = ProcessHelper.StartProcess(
    toolPath,
    ["--configure", "value with spaces"],
    requireElevation: true,
    waitForCompletion: true);

ProcessOutput output = ProcessHelper.GetShellOutput("system-tool --status", requireElevation: true);
Console.Write(output.StandardOutput);

ProcessOutput asyncOutput = await ProcessHelper.GetShellOutputAsync(
    "system-tool --status",
    cancellationToken: cancellationToken);
```

Use `ProcessHelper.StartHostProcess(...)` or `StartHostProcessAsync(...)` for commands that must escape a Flatpak
sandbox. They use `flatpak-spawn --host` when `FLATPAK_ID` is present and otherwise start the command normally. The
Flatpak manifest must grant `--talk-name=org.freedesktop.Flatpak`.

For custom working directories, environment variables, and other process settings, use
`ProcessHelper.CreateProcessStartInfo(...)` or `ProcessHelper.CreateShellProcessStartInfo(...)`, configure the returned
`ProcessStartInfo`, and pass it to the `StartProcess` or `GetProcessOutput` sync/async overload.

Create and restore profile backups:

```csharp
var backupPath = ApplicationBackup.Create();
ApplicationBackup.Restore(backupPath);

var asyncBackupPath = await ApplicationBackup.CreateAsync();
await ApplicationBackup.RestoreAsync(asyncBackupPath);
```

Export a diagnostics bundle for support:

```csharp
var bundlePath = SupportBundleExporter.Export(new SupportBundleOptions
{
    Notes = "User reported startup failure"
});

var asyncBundlePath = await SupportBundleExporter.ExportAsync();
```

Apply retention to logs and crash reports:

```csharp
ApplicationRetention.LogRetentionPolicy.MaxAge = TimeSpan.FromDays(14);
ApplicationRetention.LogRetentionPolicy.MaxFiles = 50;
ApplicationRetention.ApplyLogRetention();

ApplicationRetention.ApplyCrashReportRetention(maxCrashReports: 25, maxAge: TimeSpan.FromDays(30));
```

Track first-run and onboarding state:

```csharp
var state = OnboardingStateFile.Instance;
state.RecordLaunch();

if (state.IsFirstRun)
{
    ShowOnboarding();
    state.CompleteOnboarding("v1");
}
```

## Ignoring Known Safe Exceptions

Ignore by exception type:

```csharp
UnhandledExceptions.IgnoredExceptionList.Add(typeof(OperationCanceledException));
```

Ignore by message fragment:

```csharp
UnhandledExceptions.IgnoredExceptionMessages.Add("known benign message");
```

Avalonia DBus noise can be ignored with:

```csharp
UnhandledExceptions.IgnoreAvaloniaSafeExceptions();
```

Traverse a complete aggregate exception tree, or only its direct inner-exception chain:

```csharp
var exceptionTree = exception.EnumerateExceptions();
var innerChain = exception.EnumerateExceptions(ExceptionTraversalType.InnerExceptionChain);
```

## Application Birthday Helpers

```csharp
ApplicationKit.Birth = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

Console.WriteLine(ApplicationKit.YearsOld);
Console.WriteLine(ApplicationKit.AgeShortStr);
Console.WriteLine(ApplicationKit.IsBirthday);
```

Runtime duration since library initialization is available through:

```csharp
Console.WriteLine(ApplicationKit.RuntimeElapsed);
```

## Runtime Helpers

Use `StageKit.Runtime` when an app or library needs entry-application metadata, deployment shape detection, or a
support-friendly diagnostics report without referencing the full `StageKit` package:

```csharp
using StageKit.Runtime;

Console.WriteLine(EntryApplication.AssemblyTitle);
Console.WriteLine(EntryApplication.ExecutablePath);
Console.WriteLine(EntryApplication.PackagingType);
Console.WriteLine(RuntimeDiagnostics.GetReport());
```

`EntryApplication.ExecutablePath` prefers bundle-aware entry paths such as AppImage and macOS `.app` paths, then falls
back through single-file, hosted `dotnet`, process path, and `Environment.GetCommandLineArgs()[0]` discovery when
needed.

Relaunch with separately passed arguments when values should not be manually quoted:

```csharp
EntryApplication.LaunchNewInstance("--profile", profileName);
```

Append the loaded assembly list only when needed because it can be long:

```csharp
var report = RuntimeDiagnostics.GetReport(includeLoadedAssemblies: true);
```

## Demo

The [StageKit.Demo Avalonia app](demo/StageKit.Demo/StageKit.Demo.csproj) is an interactive workshop with tabs for:

- runtime and packaging diagnostics, including a privileged process-output sample;
- atomic autosave settings, a live System/Light/Dark theme selector, recent-document collection persistence, and direct access to the settings directory;
- profile backups, support bundles, retention, onboarding state, and a fatal crash/relaunch/report-loading round trip;
- Updatum release discovery against `sn4k3/UVtools`, checksum-verified downloads, and opt-in staged installation for the current runtime.

Update installation is opt-in and clearly separated from download, so the demo can exercise the full replacement flow
without installing anything accidentally.

The original console sample is retained as
[StageKit.DemoCmd](demo/StageKit.DemoCmd/StageKit.DemoCmd.csproj). It covers startup configuration, Serilog integration,
settings persistence, panic-save registration, crash-report launch handling, and debounced saves.

[demo/Updatum.Demo/Program.cs](demo/Updatum.Demo/Program.cs) is a "fake app" demo for the updater.

Run them with:

```bash
dotnet run --project demo/StageKit.Demo/StageKit.Demo.csproj
dotnet run --project demo/StageKit.DemoCmd/StageKit.DemoCmd.csproj
dotnet run --project demo/Updatum.Demo/Updatum.Demo.csproj
```

## Development

Restore, build, and test:

```powershell
dotnet restore
dotnet build .\StageKit.slnx --configuration Release -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
dotnet run --project .\tests\StageKit.Tests\StageKit.Tests.csproj --framework net10.0 --configuration Release --no-build --no-restore -- -noLogo -noColor -parallelMode none
dotnet run --project .\tests\StageKit.Demo.Tests\StageKit.Demo.Tests.csproj --framework net10.0 --configuration Release --no-build --no-restore -- -noLogo -noColor -parallelMode none
dotnet run --project .\tests\StageKit.Updatum.Tests\StageKit.Updatum.Tests.csproj --framework net10.0 --configuration Release --no-build --no-restore -- -noLogo -noColor -parallelMode none
dotnet run --project .\tests\StageKit.Fallout.Tests\StageKit.Fallout.Tests.csproj --framework net10.0 --configuration Release --no-build --no-restore -- -noLogo -noColor -parallelMode none
```

## Build Pipeline

Release publishing is driven by [`StageKit.Fallout`](src/StageKit.Fallout/README.md), a reusable Fallout (NUKE-style)
build library. The manual GitHub Actions release workflow packages the libraries, publishes runtime assets for each
supported platform, and attaches both to a GitHub release. `build.ps1` / `build.sh` bootstrap the .NET SDK when needed,
restore the pinned `fallout` global tool, then run the build project in `builds/build`:

```powershell
./build.ps1 Print
./build.ps1 Compile
./build.ps1 Publish
./build.ps1 GenerateInstallScript
```

```bash
./build.sh Publish
./build.sh GenerateInstallScript
```

`Publish` builds every runtime identifier in `RIds` (`win`/`osx`/`linux` × `x64`/`arm64` by default) as self-contained
ReadyToRun output, then creates the selected bundles in `artifacts/publish/`. Portable zip, .NET single-file, WiX
installer, macOS `.app`, and Linux AppImage are enabled by default. Bundles whose host requirement is unmet are skipped
with a warning. See the
[`StageKit.Fallout` README](src/StageKit.Fallout/README.md) for targets, parameters, and customization points.
`GenerateInstallScript` creates standalone Bash and Windows PowerShell installers under `scripts/` that resolve the best
compatible selected package from a GitHub release.

## Security

Please report vulnerabilities privately. See [SECURITY.md](SECURITY.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

StageKit is licensed under the [MIT License](LICENSE).
