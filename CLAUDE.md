# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

Multi-targets `net8.0` and `net10.0`. Tests are xUnit v3.

```powershell
dotnet restore
dotnet build .\StageKit.slnx -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
dotnet test .\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

Run a single test:

```powershell
dotnet test .\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ApplicationKitBirthdayTests.AgeShortStr_WhenBornToday_ReturnsZeroYears"
```

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, signs assemblies with `StageKit.snk`, and routes build output to `artifacts/`. Debug configuration disables `IsPackable`. NuGet packing only runs in Release.

## Architecture

StageKit is a small standalone .NET infrastructure library. Core application infrastructure APIs live in `StageKit`; dependency-light reusable helpers live in `StageKit.Primitives`; runtime and entry-application helpers live in `StageKit.Runtime`. Small supporting core APIs live in `StageKit.Extensions` and `StageKit.Interfaces`. The core pieces interlock through `ApplicationKit` static configuration:

- **`ApplicationKit`** (partial class, split with `ApplicationKit.Birthday.cs`) — process-wide config: `ApplicationName`, `ApplicationArgs`, `Logger`, `ProfilePath`/`ConfigsPath`/`LogsPath`, shared `JsonSerializerOptions`, startup timestamp/runtime duration, birthday helpers, and crash-report-flag parsing. `ApplicationArgs` setter auto-detects `--crash-report <id>` and populates `HasCrashReportFlag` + `CrashReportIndex`; `CrashReport` resolves the active report when possible. Default `ProfilePath` is OS-aware (Windows/Linux: `ApplicationData`; macOS: `~/Library/Application Support`).

- **Settings hierarchy**: `SubSettings` (`CommunityToolkit.Mvvm.ComponentModel.ObservableObject`) → `RootSettingsFile<T>` (singleton JSON file) and `RootCollectionFile<T,TO>` (singleton list-backed file). `RootSettingsFile<T>` uses `Lazy<T>` for `Instance`, has `LoadOrCreate` that backs up corrupt files, migrates older `SettingsVersion` values through `MigrateSettings(...)`, backs up future-version files as unsupported, then runs `ValidateSettings(...)`. It exposes `Save`/`DebouncedSave` (default 1000 ms), `CancelDebouncedSave`, `IsDebounceSavePending`, `WaitForDebouncedSaveAsync`, `SuspendAutoSave(...)`, and `BatchUpdate(...)`. `AutoSave` defaults to false and only reacts after `IsLoaded` is true. Saves are guarded by a single `_saveLock` (uses `Lock` on net10+, `object` on net8). Set `FileName`, `DirectoryPath` (defaults to `ApplicationKit.ProfilePath`), `JsonOptions`, and save behavior in constructors/init properties; override `BeforeSave`, `AfterSave`, `CurrentSettingsVersion`, `MigrateSettings`, or `ValidateSettings` to customize behavior.

- **Crash reporting**: `ExceptionInfo` captures one exception (type, message, stack, data, source), `CrashReport` walks `InnerException`/`AggregateException` chains and adds runtime/process info, `CrashReportsFile` is a `RootCollectionFile` of crash reports (opt-in via `CrashReportsFile.IsEnabled`).

- **Storage utilities**: `ApplicationBackup` creates/restores profile zip backups. `SupportBundleExporter` writes manifest/config/log support bundles. `ApplicationRetention` applies log file and crash report retention. `OnboardingStateFile` persists first-run/onboarding completion state as a `RootSettingsFile<T>`. Atomic writes and low-level IO helpers live in `StageKit.Primitives` as `SafeFile`, `SafeFileStream`, `PathUtilities`, `TemporaryDirectory`, and `TemporaryFile`.

- **`UnhandledExceptions`**: registers `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` handlers (idempotent, lock-guarded). Filters via `IgnoredExceptionList` (types) and `IgnoredExceptionMessages` (case-insensitive substrings). `HandleCrashReport` callback returning `false` lets StageKit relaunch the app with `CrashReportFlag`. `SettingsFilesToSaveBeforeCrash` stores `StageKit.Interfaces.ISavable` and calls `Save()` before `Environment.Exit`. **Configure ignore/save lists during startup only** — they are not thread-safe under concurrent exception handling.

- **CommunityToolkit.Mvvm** is a runtime package reference. StageKit-derived classes can use `[ObservableProperty]`, but consuming apps that use generator attributes should reference `CommunityToolkit.Mvvm` directly so analyzers/source generators run in that project.

The main `StageKit` package dependencies are `CommunityToolkit.Mvvm`, `ObservableCollections`, and `Microsoft.Extensions.Logging.Abstractions`. `StageKit.Primitives` and `StageKit.Runtime` should stay zero-runtime-dependency packages unless there is a strong reason. SourceLink is enabled for Release packages.

- **`ApplicationInstanceGuard`**: direct named-mutex single-instance helper with `Acquire(instanceName)`, `IsPrimary`, and `IsSecondary`. Dispose can run on any thread: it closes the mutex handle (not thread-affine) instead of calling the thread-affine `ReleaseMutex`; if still owned, the OS marks the mutex abandoned and the next `Acquire` treats `AbandonedMutexException` as primary. It intentionally has no IPC or activation forwarding yet.

- **`StageKit.Runtime`**: `EntryApplication` exposes entry assembly metadata, process/executable paths, runtime identifier, bundle detection for .NET single-file, AppImage, Flatpak, and macOS `.app`, loaded assembly formatting, and best-effort relaunch. `RuntimeDiagnostics` combines BCL runtime/process information with `EntryApplication` data for logs, support bundles, and crash reports.

## Code Conventions

From `CONTRIBUTING.md` — enforced by review, not tooling:

- File-scoped namespaces (`namespace StageKit;`), nullable enabled, `LangVersion=latest`.
- XML doc comments (`///`) required on **all public members** (warnings-as-errors will catch missing ones once `GenerateDocumentationFile=True`).
- Private fields `_camelCase`. Use `#region` blocks in larger files.
- Prefix factory methods with `Create`; query/measurement methods with `Get`.
- Use C# 14 explicit `extension` blocks for new extensions where appropriate.
- Multi-target compatibility: when using `Lock`, gate with `#if NET10_0_OR_GREATER` and fall back to `object` for net8.0 (see `RootSettingsFile`, `UnhandledExceptions`).
- Preserve each file's existing line endings when editing.
