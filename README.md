# StageKit

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/master/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit?style=for-the-badge)](https://www.nuget.org/packages/StageKit)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit is a lightweight .NET application infrastructure library for JSON settings files, observable settings objects, crash report capture, application runtime metadata, and unhandled exception handling.

The repository also includes smaller packages for reusable building blocks: `StageKit.Primitives` for low-level primitives and `StageKit.Runtime` for entry-application/runtime inspection helpers.

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
- Dependency-light primitives package for atomic file writes and disposable/resource helpers
- First-run and onboarding state persistence
- Serializable crash reports with exception chains, stack traces, runtime information, and process stats
- AppDomain and task scheduler unhandled exception helpers
- Panic-save support for registered `ISavable` settings before forced process exit
- Configurable profile, config, and log paths
- Small application "birthday" helpers for version/about screens

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

## Requirements

- .NET 8 or newer
- C# latest language version

## Packages

- `StageKit` - application infrastructure: settings, crash reports, retention, backups, support bundles, single-instance guards, and app metadata.
- `StageKit.Primitives` - dependency-light primitives: `SafeFile`, `SafeFileStream`, `PathUtilities`, `TemporaryDirectory`, `TemporaryFile`, `DisposableObject`, `LeaveOpenDisposableObject`, and `GCSafeHandle`.
- `StageKit.Runtime` - entry-application and runtime helpers: assembly metadata, process paths, bundle detection, relaunch utilities, and combined diagnostics through `RuntimeDiagnostics`.

## Quick Start

Configure StageKit during application startup:

```csharp
using StageKit;

ApplicationKit.ApplicationName = "MyApp";
ApplicationKit.ApplicationArgs = args;
ApplicationKit.Logger = logger;
ApplicationKit.UiFrameworkInfo = $"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version!.ToString(3)}";
ApplicationKit.Birth = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

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

`AutoSave` defaults to `false`. It starts reacting only after the settings object is loaded, so property changes caused by JSON hydration or `OnLoaded(...)` do not rewrite the file during startup.

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

`Save()` cancels any pending debounced save before writing. `SaveCount` tracks successful saves for the current in-memory instance and is ignored in JSON. `CancelDebouncedSave()` cancels a scheduled save without writing.

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

`BatchUpdate(...)` and `SuspendAutoSave(...)` still mark the file dirty while AutoSave is suspended. When the outermost scope exits, StageKit schedules one debounced save if changes happened and saving on dispose is enabled.

By default, settings are stored under:

```csharp
ApplicationKit.ProfilePath
```

Override `DirectoryPath` or set `ApplicationKit.ProfilePath` to customize storage.

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

`SettingsVersion` is serialized with each settings file. Older files are migrated to `CurrentSettingsVersion` and kept dirty so the upgraded schema can be persisted. If a file was written by a newer app version, StageKit renames it to `<file>.unsupported-version-<timestampUtc>` and falls back to a fresh instance.

If a settings file fails to deserialize on load (corrupt JSON), StageKit renames it to `<file>.corrupt-<timestampUtc>` and falls back to a fresh instance. Original data is preserved on disk for inspection or recovery.

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

`RootCollectionFile<T, TO>` exposes `Items` as an `ObservableList<TO>` and `ItemsView` as a synchronized view for UI binding. Set `TrackItemsWithChangeNotification = true` in the constructor when collection items implement `INotifyPropertyChanged` and item property changes should mark the file dirty and trigger `AutoSave`; keep it disabled for immutable items or very large collections.

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

Classes deriving from `SubSettings`, `RootSettingsFile<T>`, or `RootCollectionFile<T, TO>` can use CommunityToolkit's `[ObservableProperty]` generator:

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

StageKit references `CommunityToolkit.Mvvm`, but if your app uses generator attributes such as `[ObservableProperty]`, reference `CommunityToolkit.Mvvm` directly in that app too so the analyzer/source generator runs in the consuming project.

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

Register settings that should be saved before StageKit forces process exit after a fatal exception:

```csharp
UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(AppSettings.Instance);
```

`SettingsFilesToSaveBeforeCrash` is a `HashSet<StageKit.Interfaces.ISavable>`, so any type implementing `ISavable` can participate.

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

The guard uses a named mutex. Because .NET mutex ownership is thread-affine, dispose the guard on the same thread that acquired it. It does not forward activation arguments yet, but the API is shaped so named-pipe activation forwarding can be added later.

If your app also launches a crash-report viewer with `ApplicationKit.CrashReportFlag`, check the crash-report mode before blocking secondary instances, or use a different instance name for the viewer process.

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
var exceptionTree = UnhandledExceptions.TraverseExceptions(exception);
var innerChain = UnhandledExceptions.TraverseExceptions(
    exception,
    ExceptionTraversalType.InnerExceptionChain);
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

Use `StageKit.Runtime` when an app or library needs entry-application metadata, deployment shape detection, or a support-friendly diagnostics report without referencing the full `StageKit` package:

```csharp
using StageKit.Runtime;

Console.WriteLine(EntryApplication.AssemblyTitle);
Console.WriteLine(EntryApplication.BundleType);
Console.WriteLine(RuntimeDiagnostics.GetReport());
```

Append the loaded assembly list only when needed because it can be long:

```csharp
var report = RuntimeDiagnostics.GetReport(includeLoadedAssemblies: true);
```

## Demo

See [StageKit.Demo/Program.cs](StageKit.Demo/Program.cs) for a runnable console demo covering startup configuration, Serilog integration, AutoSave settings, collection settings, panic-save registration, crash report launch handling, and debounced save waiting.

Run it with:

```bash
dotnet run --project StageKit.Demo/StageKit.Demo.csproj
```

## Development

Restore, build, and test:

```powershell
dotnet restore
dotnet build .\StageKit.slnx -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
dotnet test .\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

## Security

Please report vulnerabilities privately. See [SECURITY.md](SECURITY.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

StageKit is licensed under the [MIT License](LICENSE).
