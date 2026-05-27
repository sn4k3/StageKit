# AGENTS.md

This file gives Codex repo-specific guidance for working on StageKit.

## Project Shape

StageKit is a small standalone .NET infrastructure library for application settings, crash reporting, runtime metadata, backups, support bundles, retention, onboarding state, and single-instance guards.

Main projects:

- `StageKit/StageKit.csproj` - library
- `StageKit.Primitives/StageKit.Primitives.csproj` - dependency-light primitives and IO helpers
- `StageKit.Runtime/StageKit.Runtime.csproj` - runtime and entry-application helpers
- `StageKit.Tests/StageKit.Tests.csproj` - xUnit v3 tests
- `StageKit.Demo/StageKit.Demo.csproj` - demo app

The solution is `StageKit.slnx`.

## Build And Test

Use these commands from the repository root:

```powershell
dotnet restore
dotnet build .\StageKit.slnx -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
dotnet test .\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

Run a single test with:

```powershell
dotnet test .\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~TestOrClassName"
```

The repo multi-targets `net8.0` and `net10.0`. Treat warnings as errors is enabled, documentation files are generated, and assemblies are signed with `StageKit.snk`.

## Coding Rules

- Use file-scoped namespaces.
- Nullable reference types are enabled.
- `LangVersion` is `latest`.
- Use modern C# syntax when it improves clarity.
- Public APIs must have XML documentation comments.
- Keep public APIs minimal and documented.
- Private fields use `_camelCase`.
- Keep edits focused. Do not do unrelated refactors while fixing a bug or adding a feature.
- Preserve existing line endings and local style in touched files.
- Use `#region` blocks where surrounding larger files already use them.
- Prefer factory methods named `Create...`.
- Prefer query or measurement methods named `Get...`.
- Prefer `ArgumentNullException.ThrowIfNull(...)` for guard clauses.
- Use C# 14 explicit `extension` blocks for new extension APIs when appropriate.
- Prefer `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and pooling only when there is a clear benefit.
- Avoid unnecessary allocations in hot paths.
- Do not over-engineer.

## Multi-Targeting Notes

When using APIs that differ between target frameworks, preserve `net8.0` compatibility.

For `Lock`, follow the existing pattern:

```csharp
#if NET10_0_OR_GREATER
private readonly Lock _lock = new();
#else
private readonly object _lock = new();
#endif
```

Do not introduce `net10.0`-only APIs into shared code unless they are guarded.

Maintain multi-targeting compatibility when changing library code.

## Architecture Notes

Core application infrastructure APIs live in `StageKit`. Low-level reusable helpers live in `StageKit.Primitives`. Runtime and entry-application inspection helpers live in `StageKit.Runtime`. Smaller supporting core APIs live in `StageKit.Extensions` and `StageKit.Interfaces`.

`ApplicationKit` is process-wide configuration. It owns app name, args, logger, profile/config/log paths, JSON options, runtime timestamps, birthday helpers, and crash-report flag parsing.

Settings types follow this hierarchy:

- `SubSettings`
- `RootSettingsFile<T>`
- `RootCollectionFile<T, TO>`

`RootSettingsFile<T>` handles singleton JSON persistence, migration, validation, autosave, debounced save, corrupt-file backup, unsupported-version backup, and save hooks.

`RootCollectionFile<T, TO>` is for list-backed settings files and exposes `Items` plus `ItemsView`.

Crash reporting flows through `ExceptionInfo`, `CrashReport`, `CrashReportsFile`, and `UnhandledExceptions`.

Storage helpers in `StageKit` include `ApplicationBackup`, `SupportBundleExporter`, `ApplicationRetention`, and `OnboardingStateFile`.

`StageKit.Primitives` contains `DisposableObject`, `LeaveOpenDisposableObject`, `GCSafeHandle`, `SafeFile`, `SafeFileStream`, `PathUtilities`, `TemporaryDirectory`, and `TemporaryFile`. Keep this package dependency-light and suitable for reuse by other libraries. IO-related files may live under the `IO/` folder, but their public namespace should remain `StageKit.Primitives` unless the IO surface becomes large enough to justify a separate namespace.

`StageKit.Runtime` contains `EntryApplication`, `ApplicationBundleType`, and `RuntimeDiagnostics`. Keep this package focused on runtime, process, entry assembly, bundle detection, relaunch, and diagnostics helpers.

`ApplicationInstanceGuard` is a direct named-mutex single-instance helper. Mutex ownership is thread-affine, so dispose must happen on the same thread that acquired the guard.

## Behavioral Constraints

- `AutoSave` defaults to `false` and should only react after settings are loaded.
- Saves should remain atomic.
- Corrupt settings files should be preserved on disk with a `.corrupt-<timestampUtc>` suffix.
- Settings written by a newer app version should be preserved with an `.unsupported-version-<timestampUtc>` suffix.
- `WaitForDebouncedSaveAsync` must not report success while a save is still in progress.
- `UnhandledExceptions.IgnoredExceptionList`, `IgnoredExceptionMessages`, and `SettingsFilesToSaveBeforeCrash` should be configured during startup, not concurrently with exception handling.

## Package Dependencies

Runtime dependencies are intentionally small:

- `CommunityToolkit.Mvvm`
- `ObservableCollections`
- `Microsoft.Extensions.Logging.Abstractions`

`StageKit.Primitives` and `StageKit.Runtime` should stay zero-runtime-dependency packages unless there is a strong justification.

`Microsoft.SourceLink.GitHub` is a private build/package dependency.

Avoid adding new dependencies unless the benefit is clear for a small infrastructure library.

Prefer zero-dependency implementations for small utilities.

Keep package metadata valid when editing project or package settings.

## Documentation Expectations

Update `README.md` when adding or changing user-facing APIs. Also update package-specific READMEs such as `StageKit.Primitives/README.md` or `StageKit.Runtime/README.md` when those packages change.

Update `CHANGELOG.md` for notable changes. Existing entries are newest-first.

Do not rename public types or methods without noting it as a breaking change.

Avoid breaking changes unless explicitly requested.

Update XML docs when public APIs change.

Public API changes usually need:

- XML docs
- Focused tests
- README sample or note if the API is user-facing
- Changelog entry

## Testing Guidance

Prefer focused tests in `StageKit.Tests` for the changed behavior. For persistence-related tests, assert both in-memory behavior and filesystem side effects when relevant.

Before finishing code changes, run at least the targeted test command. For broad public API changes, run the full test project.

## Codex Workflow Hints

- Read the nearby implementation and tests before editing.
- Use existing StageKit patterns instead of introducing new abstractions.
- Keep public API naming conservative and consistent with current names.
- Do not rewrite generated artifacts or package metadata unless the task requires it.
- If NuGet restore/network access fails, retry the same build or test command with approval rather than changing project files to work around the environment.
