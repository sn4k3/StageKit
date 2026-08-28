# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

Library projects multi-target `net8.0` and `net10.0` (see `Directory.Build.props`). Test and tool projects target `net10.0` only. Tests are xUnit v3 (run as executables via the Microsoft Testing Platform).

```powershell
dotnet restore
dotnet build .\StageKit.slnx -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
dotnet test .\tests\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

CI runs the test assemblies directly (matches `.github/workflows/dotnet.yml`):

```powershell
dotnet run --project .\tests\StageKit.Tests\StageKit.Tests.csproj --framework net10.0 --configuration Release --no-build -- -noLogo -noColor -parallel none -automated sync
```

Test projects: `tests/StageKit.Tests` (core + Primitives + Runtime), `tests/StageKit.Updatum.Tests`, `tests/StageKit.Fallout.Tests`.

Run a single test:

```powershell
dotnet test .\tests\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ApplicationKitBirthdayTests.AgeShortStr_WhenBornToday_ReturnsZeroYears"
```

Run a demo:

```powershell
dotnet run --project .\demo\StageKit.Demo\StageKit.Demo.csproj
dotnet run --project .\demo\Updatum.Demo\Updatum.Demo.csproj
```

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, strong-name-signs every assembly with `StageKit.snk`, emits reference assemblies, and routes build output to `artifacts/`. Debug disables `IsPackable`; NuGet packing only runs in Release. `TreatWarningsAsErrors` means missing XML doc comments on public members are build breaks in packable projects (`GenerateDocumentationFile=True`).

## Release / Publish Pipeline (Fallout)

`build.ps1` / `build.sh` bootstrap the .NET SDK if absent, run `dotnet tool restore` (installs the `fallout` global tool, pinned in `.config/dotnet-tools.json`), then invoke `dotnet fallout <args>`. Fallout compiles and runs `builds/build/build.csproj` (`Build : StageKitBuild`), configured by `.fallout/parameters.json` (points at `StageKit.slnx` and the build project).

`StageKitBuild` (in `src/StageKit.Fallout`, a reusable NUKE-style build library on `Fallout.Common`/`Fallout.Utilities`) resolves software metadata from the main project's MSBuild properties and exposes targets: `Print`, `Clean`, `Restore`, `Compile`, `Run`, `Publish`. `Publish` publishes each RID in `RIds` (default: win/osx/linux × x64/arm64) self-contained + ReadyToRun and produces the bundle formats in `PublishBundles`: portable zip, .NET single-file, WiX installer (`builds/StageKit.Installer`), macOS `.app`, Linux AppImage. Output lands in `artifacts/publish/`.

The `Build` subclass in `builds/build/Build.cs` shows the customization surface: `SoftwareName`, `ExcludedProjectNameTokens` (tokens that disqualify a project from being auto-detected as `MainProject` — e.g. `test`, `demo`, `build`, `fake`), `BeforePublishRid`/`AfterPublishRid` callbacks, and `Create*AppBundleOptions` overrides.

`.github/workflows/release.yml` (manual `workflow_dispatch`, dry-run by default) is a separate, simpler path: it packs `StageKit`, `StageKit.Primitives`, `StageKit.Runtime` and pushes to NuGet.org + GitHub Packages, tags `v<Version>`, and cuts a GitHub release from the top `CHANGELOG.md` section.

## Repository Layout

```
src/StageKit              core application infrastructure (packed)
src/StageKit.Primitives   dependency-light reusable helpers (packed)
src/StageKit.Runtime      runtime / entry-application helpers (packed)
src/StageKit.Updatum      GitHub release updater (packed)
src/StageKit.Fallout      build-pipeline library (not packed; Fallout-based)
demo/StageKit.Demo        console demo for core StageKit
demo/Updatum.Demo         console "fake app" demo for the updater (ProductName Updatum.FakeApp)
builds/build              Fallout build entry point (builds/build/Build.cs)
builds/StageKit.Installer WiX installer project (.wixproj)
tests/*                   xUnit v3 test suites
```

`Extensions/` and `Interfaces/` folders inside a project are namespace folders, not sub-packages.

## Architecture

The core pieces interlock through `ApplicationKit` static configuration:

- **`ApplicationKit`** (partial, split with `ApplicationKit.Birthday.cs`) — process-wide config: `ApplicationName`, `ApplicationArgs`, `Logger`, `ProfilePath`/`ConfigsPath`/`LogsPath`, shared `JsonSerializerOptions`, startup timestamp / runtime duration, `IsPortable` + portable profile-path parsing (`--profile-path`, `--portable`), birthday helpers, and crash-report-flag parsing. `ApplicationArgs` setter auto-detects `--crash-report <id>` and populates `HasCrashReportFlag` + `CrashReportIndex`. Default `ProfilePath` is OS-aware (Windows/Linux: `ApplicationData`; macOS: `~/Library/Application Support`). `LaunchNewInstanceKeepApplicationArgs(...)` relaunches while preserving startup args minus the executable path and any crash-report pair.

- **Settings hierarchy**: `SubSettings` (`CommunityToolkit.Mvvm.ComponentModel.ObservableObject`) → `RootSettingsFile<T>` (singleton JSON file) and `RootCollectionFile<T,TO>` (singleton list-backed file). `RootSettingsFile<T>` uses `Lazy<T>` for `Instance`; `LoadOrCreate` backs up corrupt files as `<file>.corrupt-<timestampUtc>`, migrates older `SettingsVersion` through `MigrateSettings(...)`, backs up future-version files as `<file>.unsupported-version-<timestampUtc>`, then runs `ValidateSettings(...)`. Exposes `Save`/`DebouncedSave` (default 1000 ms), `CancelDebouncedSave`, `IsDebounceSavePending`, `WaitForDebouncedSaveAsync`, `SuspendAutoSave(...)`, `BatchUpdate(...)`. `AutoSave` defaults to false and only reacts after `IsLoaded`. Saves are guarded by a single `_saveLock` (`Lock` on net10+, `object` on net8). Override `BeforeSave`, `AfterSave`, `CurrentSettingsVersion`, `MigrateSettings`, `ValidateSettings` to customize.

- **Crash reporting**: `ExceptionInfo` captures one exception; `CrashReport` walks `InnerException`/`AggregateException` chains and adds runtime/process/GC info; `CrashReportsFile` is a `RootCollectionFile` of crash reports (opt-in via `CrashReportsFile.IsEnabled`).

- **`UnhandledExceptions`**: registers `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` handlers (idempotent, lock-guarded). Filters via `IgnoredExceptionList` (types) and `IgnoredExceptionMessages` (case-insensitive substrings). `HandleCrashReport` returning `false` lets StageKit relaunch the app with `CrashReportFlag`. `SettingsFilesToSaveBeforeCrash` holds `StageKit.Interfaces.ISavable` and calls `Save()` before `Environment.Exit`. Unobserved task exceptions are non-fatal unless `UnobservedTaskExceptionIsTerminating` is set. **Configure ignore/save lists during startup only** — not thread-safe under concurrent exception handling.

- **Storage utilities**: `ApplicationBackup` (profile zip backup/restore), `SupportBundleExporter` (manifest/config/log bundle), `ApplicationRetention` (log + crash-report retention), `OnboardingStateFile` (first-run state as a `RootSettingsFile<T>`). Atomic writes and low-level IO live in `StageKit.Primitives/IO`: `SafeFile`, `SafeFileStream`, `PathUtilities`, `FileUtilities`, `TemporaryDirectory`, `TemporaryFile`. Disposal family in `StageKit.Primitives`: `DisposableObject` → `UnmanagedDisposableObject` (finalizer fallback) and `LeaveOpenDisposableObject`; plus `GCSafeHandle`, `UnmanagedMemoryManager<T>`, `StringExtensions` (quoting).

- **`ApplicationInstanceGuard`**: named-mutex single-instance helper — `Acquire(name)` / `AcquireGlobal(name)`, `IsPrimary`, `IsSecondary`, `PrimaryProcess`. Dispose closes the mutex handle (not thread-affine) rather than calling `ReleaseMutex`; an abandoned mutex is treated as primary on next `Acquire`. No IPC / activation forwarding yet.

- **`StageKit.Runtime`**: `EntryApplication` — entry assembly metadata, process/executable paths, RID, and `PackagingType` (`ApplicationPackagingType` `[Flags]`: `Portable`, `DotNetSingleFile`, `WindowsInstaller`, `LinuxAppImage`, `LinuxFlatpak`, `MacOSAppBundle`) via bundle detection for single-file, AppImage, Flatpak, and macOS `.app`. `ExecutablePath` is bundle-aware and falls back to `Environment.GetCommandLineArgs()[0]`. Prefer `LaunchNewInstance(params string[])` over the raw string overload. `RuntimeDiagnostics` combines BCL runtime/process info with `EntryApplication` for logs, support bundles, and crash reports.

- **`StageKit.Updatum`** (`Octokit`-backed): `UpdatumManager(owner, repo)` discovers a compatible GitHub release asset (`AssetRegexPattern`, `AssetExtensionFilter`), downloads it into an isolated temp workspace, and stages a cross-platform update. Verification: GitHub-native `sha256:` digest, falling back to a `<asset-name>.sha256` sidecar (`RequireAssetChecksum` / `AssetSignatureVerifier` for Authenticode / macOS signing). Operations are serialized; `State`/`IsBusy` stay active until async work completes; property notifications dispatch through the captured synchronization context. Rejects unsafe asset names; portable/single-file scripts stage replacements and restore backups on failed commit; honors cancellation, `forceTerminate`, and no-relaunch.

Package runtime dependencies: `StageKit` → `CommunityToolkit.Mvvm`, `ObservableCollections`, `Microsoft.Extensions.Logging.Abstractions`. `StageKit.Updatum` → `Octokit`. Keep `StageKit.Primitives` and `StageKit.Runtime` zero-runtime-dependency unless there is a strong reason. SourceLink is on for Release packages. `StageKit.Updatum` and `StageKit.Fallout` grant `InternalsVisibleTo` their test projects.

- **CommunityToolkit.Mvvm** is a runtime reference. StageKit-derived classes can use `[ObservableProperty]`, but consuming apps that use generator attributes should reference `CommunityToolkit.Mvvm` directly so the source generator runs there.

## Code Conventions

From `CONTRIBUTING.md` — enforced by review, not tooling:

- File-scoped namespaces (`namespace StageKit;`), nullable enabled, `LangVersion=latest`, implicit usings.
- XML doc comments (`///`) required on **all public members**.
- Private fields `_camelCase`. Use `#region` blocks in larger files.
- Prefix factory methods with `Create`; query/measurement methods with `Get`.
- Use C# 14 explicit `extension` blocks for new extensions where appropriate.
- Prefer `ArgumentNullException.ThrowIfNull(...)` / `ArgumentException.ThrowIfNullOrWhiteSpace(...)` for guard clauses.
- Multi-target compatibility: gate `Lock` with `#if NET10_0_OR_GREATER`, fall back to `object` for net8.0 (see `RootSettingsFile`, `UnhandledExceptions`).
- Do not add new runtime dependencies to the packed libraries without clear benefit.
- Preserve each file's existing line endings (repo is LF) when editing.
- Keep each package's README and `CHANGELOG.md` (top `# Unreleased` section) updated when changing user-facing APIs.
