# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

Multi-targets `net8.0` and `net10.0`. Tests are xUnit v3.

```bash
dotnet restore
dotnet build StageKit/StageKit.csproj
dotnet test StageKit.Tests/StageKit.Tests.csproj
```

Run a single test:

```bash
dotnet test StageKit.Tests/StageKit.Tests.csproj --filter "FullyQualifiedName~ApplicationKitBirthdayTests.AgeShortStr_WhenBornToday_ReturnsZeroYears"
```

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, signs assemblies with `StageKit.snk`, and routes build output to `artifacts/`. Debug configuration disables `IsPackable`. NuGet packing only runs in Release.

## Architecture

StageKit is a small standalone .NET infrastructure library. Most public APIs live in `StageKit`; small supporting APIs live in `StageKit.Extensions` and `StageKit.Interfaces`. The pieces interlock through `ApplicationKit` static configuration:

- **`ApplicationKit`** (partial class, split with `ApplicationKit.Birthday.cs`) — process-wide config: `ApplicationName`, `ApplicationArgs`, `Logger`, `ProfilePath`/`ConfigPath`/`LogsPath`, shared `JsonSerializerOptions`, startup timestamp/runtime duration, birthday helpers, and crash-report-flag parsing. `ApplicationArgs` setter auto-detects `--crash-report <id>` and populates `HasCrashReportFlag` + `CrashReportIndex`; `CrashReport` resolves the active report when possible. Default `ProfilePath` is OS-aware (Windows/Linux: `ApplicationData`; macOS: `~/Library/Application Support`).

- **Settings hierarchy**: `SubSettings` (`CommunityToolkit.Mvvm.ComponentModel.ObservableObject`) → `RootSettingsFile<T>` (singleton JSON file) and `RootCollectionFile<T,TO>` (singleton list-backed file). `RootSettingsFile<T>` uses `Lazy<T>` for `Instance`, has `LoadOrCreate` that swallows deserialization errors via `UnhandledExceptions.HandleSafeException`, and exposes `Save`/`DebouncedSave` (default 1000 ms), `CancelDebouncedSave`, `IsDebounceSavePending`, and `WaitForDebouncedSaveAsync`. `AutoSave` defaults to false and only reacts after `IsLoaded` is true. Saves are guarded by a single `_saveLock` (uses `Lock` on net10+, `object` on net8). Override `FileName`, `DirectoryPath` (defaults to `ApplicationKit.ConfigPath`), `JsonOptions`, `BeforeSave`, or `AfterSave` to customize.

- **Crash reporting**: `ExceptionInfo` captures one exception (type, message, stack, data, source), `CrashReport` walks `InnerException`/`AggregateException` chains and adds runtime/process info, `CrashReportsFile` is a `RootCollectionFile` of crash reports (opt-in via `CrashReportsFile.IsEnabled`).

- **`UnhandledExceptions`**: registers `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` handlers (idempotent, lock-guarded). Filters via `IgnoredExceptionList` (types) and `IgnoredExceptionMessages` (case-insensitive substrings). `HandleCrashReport` callback returning `false` lets StageKit relaunch the app with `CrashReportFlag`. `SettingsFilesToSaveBeforeCrash` stores `StageKit.Interfaces.ISavable` and calls `Save()` before `Environment.Exit`. **Configure ignore/save lists during startup only** — they are not thread-safe under concurrent exception handling.

- **CommunityToolkit.Mvvm** is a runtime package reference. StageKit-derived classes can use `[ObservableProperty]`, but consuming apps that use generator attributes should reference `CommunityToolkit.Mvvm` directly so analyzers/source generators run in that project.

Runtime package dependencies are `CommunityToolkit.Mvvm`, `ObservableCollections`, and `Microsoft.Extensions.Logging.Abstractions`. SourceLink is enabled for Release packages.

## Code Conventions

From `CONTRIBUTING.md` — enforced by review, not tooling:

- File-scoped namespaces (`namespace StageKit;`), nullable enabled, `LangVersion=latest`.
- XML doc comments (`///`) required on **all public members** (warnings-as-errors will catch missing ones once `GenerateDocumentationFile=True`).
- Private fields `_camelCase`. Use `#region` blocks in larger files.
- Prefix factory methods with `Create`; query/measurement methods with `Get`.
- Use C# 14 explicit `extension` blocks for new extensions where appropriate.
- Multi-target compatibility: when using `Lock`, gate with `#if NET10_0_OR_GREATER` and fall back to `object` for net8.0 (see `RootSettingsFile`, `UnhandledExceptions`).
- Preserve each file's existing line endings when editing.
