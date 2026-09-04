# v0.3.1 (04/09/2026)

- Add Fallout's `GenerateInstallScript` target, which creates GitHub Releases Bash and Windows PowerShell installers
  from the selected `PackagingTypes`, exposes command help and explicit downgrade/version selection, prioritizes native
  installers over generic bundles, keeps Portable last, lists all published release versions or their release-note
  changelogs (limited to 20 releases by default), reports clean PowerShell release-resolution errors, and falls back
  when an asset or package tool is unavailable.
- Fix macOS PKG and DMG auto-updates leaving the application closed by deferring relaunch to a non-elevated helper that
  waits for the old process to exit. Set the StageKit demo's Avalonia application name so its macOS menu uses
  `StageKit`.
- Add an `/Applications` shortcut to macOS DMG images for drag-and-drop installation; PKG installers continue to install
  their application directly in `/Applications`.
- Add opt-in `LinuxAppBundleOptions.FlatpakAllowHostCommandExecution` manifest configuration for applications that need
  the `org.freedesktop.Flatpak` host-command service.
- Stage `libicu74` in Snap packages by default so self-contained .NET applications have globalization support, with a
  configurable `LinuxAppBundleOptions.SnapStagePackages` list for other bases and runtime dependencies.
- Add `ProcessHelper` host-process factories and start helpers that use `flatpak-spawn --host` inside Flatpak and normal
  process launching elsewhere. Updatum now installs and relaunches Flatpak updates through that bridge, using its
  host-visible application cache for downloaded bundles.
- Add `HostSystem.TryFindExecutable` for subprocess-free executable lookup with Windows `PATHEXT` support and Unix
  execute-permission validation.
- Add synchronous and asynchronous `HostSystem` helpers to open URLs, directories, and files with their default host
  applications, or reveal a file in Windows Explorer and macOS Finder (with containing-directory fallback on Linux).
- Add lazy `BuildRuntime.Instance` manifest loading and `BuildRuntime.TryLoad(...)` to `StageKit.Runtime`, using
  source-generated JSON metadata and the generic enum converter for trimming and Native AOT compatibility.

# v0.3.0 (02/09/2026)

- Add `ProcessHelper` with privileged-process-aware administrator elevation through Windows `runas`, Linux `pkexec`, and
  macOS `osascript`, synchronous and asynchronous captured stdout/stderr results, and cross-platform process and shell
  helpers with cancellation support. Direct executable launches now use the modern .NET default of
  `UseShellExecute=false`, and configurable process and shell `ProcessStartInfo` factories and execution overloads
  expose working-directory, environment, and other native process settings. `CreateShellScriptProcessStartInfo` runs a
  script file by passing its path as a discrete argument, so a path containing spaces is not word-split by `bash -c`.
- Add elevation-denial detection to `ProcessHelper` so a refused administrator prompt is distinguishable from an
  ordinary command failure: named exit-code constants per platform, `IsExitCodeElevationDenied` overloads, and
  parameterless `Process` and `ProcessOutput` extension methods. A cancelled Windows `runas` prompt now returns
  `WindowsElevationCancelledExitCode` instead of the generic `-1`; other startup failures and timeouts still return
  `-1`. Updatum reports a denied package installation with a dedicated message.
- Add Linux `PackagingTypes` for Flatpak, Debian, RPM, Arch Linux binary, and Snap packages, plus macOS DMG and PKG
  output. Package creation uses the platform-native `flatpak-builder`, `dpkg-deb`, `rpmbuild`, `makepkg`, `snapcraft`,
  `hdiutil`, and `pkgbuild` tools.
- Extend Updatum asset selection and installation to Linux Flatpak, Debian, RPM, Arch Linux binary, and Snap packages,
  plus macOS PKG and DMG packages. Flatpak updates preserve the current user/system installation scope, privileged
  package installs use `ProcessHelper` elevation, and post-install completion or relaunch only continues after a
  successful installer exit code. DMG installation mounts images read-only, always detaches them, and installs an
  embedded PKG or atomically replaces an embedded app bundle with rollback. A DMG runs unprivileged first and only
  re-runs with the administrator prompt when it wraps a PKG or targets a directory the current user cannot write, so
  instance termination and
  `InstallUpdateInjectCustomScript` still run exactly once.
- Update the release workflow to build runtime assets for supported platforms, attach packages and assets to a GitHub
  release, and omit Winget publishing.
- Replace the original `StageKit.Demo` console sample with an Avalonia desktop workshop for runtime diagnostics,
  settings, storage utilities, settings-directory access, crash/relaunch report recovery, and verified Updatum downloads
  from `sn4k3/UVtools`; add live theme selection, profile-directory explorer access, and opt-in update installation;
  retain the console sample as `StageKit.DemoCmd`.
- Resolve published executables, bundle launchers, and WiX installer payloads from the main project's `AssemblyName`,
  independently of product and artifact naming.
- Rename `ApplicationBundleType` to `ApplicationPackagingType` (with `Portable`,
  `DotNetSingleFile`, `WindowsInstaller`, `LinuxAppImage`, `LinuxFlatpak`, `MacOSAppBundle`)
- Replace `EntryApplication.BundleType` with `PackagingType`
- Add `FileUtilities` and `StringExtensions` (quoting helpers) to `StageKit.Primitives`
- Add `StageKit.Updatum`: GitHub release discovery, SHA-256 verification, platform signature verification, and
  cross-platform staged update installation
- Add Bash ANSI-C and Windows batch value quoting helpers to `StageKit.Primitives.StringExtensions`; reuse shared
  leaf-name validation from `FileUtilities` in `StageKit.Updatum`
- Add `StageKit.Fallout`: NUKE-based build pipeline (`StageKitBuild`) with Windows/macOS/Linux bundle creation, AppImage
  tooling, WiX installer orchestration, and Flatpak support
- Add WiX installer project template and `builds/build/` NUKE build entry point
- Update solution file, README, CHANGELOG, and all documentation for the above changes
- Serialize updater operations and keep `State`/`IsBusy` active until asynchronous work actually completes.
- Use isolated temporary workspaces, reject unsafe release-asset names, verify GitHub-native SHA-256 digests with
  sidecars as fallback, and support application-provided platform signature verification.
- Make portable and single-file update scripts stage replacements and restore backups when commit fails.
- Honor install cancellation, `forceTerminate`, and no-relaunch behavior consistently, including bounded Flatpak
  installation that kills timed-out child processes.
- Dispatch updater property notifications through the configured synchronization context and add a dedicated Updatum
  regression test suite.

# v0.2.6 (24/08/2026)

- Fix single-file detection for publish configurations that extract assemblies to a temporary directory, plus Flatpak
  executable-path detection.

# v0.2.5 (24/08/2026)

- Add `WindowsInstaller` to `ApplicationPackagingType`.
- Fix single-file and Flatpak executable-path detection.
- Harden archive path normalization and reparse-point containment checks.
- Add `UnmanagedDisposableObject` for types requiring finalizer fallback.
- Fix unobserved task exceptions being treated as process-fatal.
- Add `UnhandledExceptions.UnobservedTaskExceptionIsTerminating` configuration.
- Preserve invalid settings files when JSON contains `null` or migration fails.
- Use the Windows global mutex namespace for `AcquireGlobal()`.
- Ensure default backup and support-bundle paths are unique.
- Validate retention limits and skip reparse-point traversal.
- Bump dependencies

# v0.2.4 (20/07/2026)

- Add `Environment.GetCommandLineArgs()[0]` fallback for `EntryApplication.ExecutablePath` when the entry assembly is
  unavailable.
- Add `ApplicationKit.LaunchNewInstanceKeepApplicationArgs(...)` to relaunch while preserving startup arguments,
  excluding the executable path and existing crash-report argument pair.
- Add `ApplicationKit.GetLaunchArgumentsKeepApplicationArgs` to retrieve the current launch arguments excluding the
  executable path and existing crash-report argument pair.
- Preserve configured startup arguments when launching the crash-report viewer.

# v0.2.3 (19/07/2026)

- Adds `ApplicationKit.IsPortable` to expose whether the app is in portable mode.
- Adds `PortableProfileDirectoryName` configuration option.
- Adds profile path parsing from command-line arguments with `--profile-path` and `--portable` flags (configurable),
  enabling flexible application data directory configuration.
- Introduces path validation utilities (`TryGetValidFullDirectoryPath`, `IsWritableOrCreatableDirectory`,
  `IsWritableDirectory`) to `PathUtilities`.
- Refactors process naming in `EntryApplication` to separate process name from Windows .exe suffix (new
  `ProcessFullName`
  property), `ProcessName` is now just the base name without the suffix as it is in `Environment.ProcessName`.
- Updates `ApplicationName` fallback chain to:
  `EntryApplication.AssemblyName ?? EntryApplication.ExecutableName ?? EntryApplication.ProcessName`

# v0.2.2 (22/06/2026)

- Add `CrashReport.AppendTextBeforeCustomData` for appending text before the custom data section in the formatted
  report.
- Add `UnmanagedMemoryManager<T>` for exposing externally owned unmanaged buffers as `Memory<T>`.

# v0.2.1 (07/06/2026)

- Add `CrashReport.GcTotalMemory`, `GcTotalAllocatedBytes`, and `GcCollectionCounts` to capture managed heap, lifetime
  allocations, and per-generation GC counts at crash time.
- Add `CrashReport.CustomData` (`IReadOnlyDictionary<string, object?>`) for application-specific key-value data,
  appended to the formatted report.
- Add `StageKitExceptionEventArgs` carrying `Category`, `IsIgnored`, and `CustomData`, with a `ToCrashReport()` helper
  and conversion constructors from `UnhandledExceptionEventArgs`/`UnobservedTaskExceptionEventArgs`.
- Add `UnhandledExceptions.ExceptionThrown` event, raised for every caught unhandled exception (fatal or ignored).
- Add `UnhandledExceptions.HandleSafeException(...)` to log a non-fatal exception at a configurable `LogLevel`.
- Add `ExceptionTraversalType` and `ExceptionExtensions.EnumerateExceptions(...)` for traversing complete exception
  trees or direct inner-exception chains, allocating a pending-branch stack only when required by branching aggregate
  exceptions.
- Breaking: replace the `ExceptionInfo` constructor's `handleAggregateExceptionAsLinkedLink` boolean with an
  `ExceptionTraversalType` parameter.
- Breaking: move `includeStackTrace` before `includeInnerException` in the `ExceptionInfo` constructor.
- Add `UnhandledExceptions.HandleUnhandledException(StageKitExceptionEventArgs, ...)` overload; non-terminating
  unhandled exceptions are now logged via `HandleSafeException` instead of terminating the process.
- Add `CrashReport` constructors taking optional `customData` and a `StageKitExceptionEventArgs`.
- Fix `ExceptionInfo` to preserve aggregate exception metadata, capture complete nested exception trees, omit null
  optional JSON properties, and avoid recursive construction for deep exception chains.
- Rename `CrashReport.ElapsedRuntime` to `ProgramElapsedRuntime`.
- `CrashReport.DateTimeUtc` is no longer `required`.
- Move `CrashReport` capture to property initializers and chain the exception constructor through the default
  constructor.
- `UnhandledExceptions` now invokes `HandleCrashReport` before persisting to `CrashReportsFile`, so the handler can
  manipulate the report before it is displayed or persisted.
- Fix `ApplicationInstanceGuard.Dispose` to be thread-safe: it disposes the mutex handle instead of calling the
  thread-affine `ReleaseMutex`, so dispose can run on any thread (OS marks the mutex abandoned, which `Acquire` already
  treats as primary).
- Improve package descriptions for `StageKit`, `StageKit.Primitives`, and `StageKit.Runtime`.

# v0.2.0 (27/05/2026)

- Add `PrimaryProcess` property to `ApplicationInstanceGuard`
- Add `StageKit.Primitives` library:
  - Move `SafeFile` to `StageKit.Primitives`
  - Add `DisposableObject` base class for disposable patterns
  - Add `LeaveOpenDisposableObject` base class for disposable patterns with optional leave-open semantics
  - Add `GCSafeHandle` for safe handle management with garbage collection support
  - Add `SafeFileStream` for safe file stream management with atomic write support
  - Add `PathUtilities` for common path operations and utilities
  - Add `TemporaryDirectory` and `TemporaryFile` for temporary file and directory management
- Add `StageKit.Runtime` library:
  - Add `EntryApplication` class with information about the entry assembly and application instance
  - Add `RuntimeDiagnostics` class for combined runtime, process, and entry-application diagnostics
  - Add `EntryApplication.LaunchNewInstance(params string[])` for safer multi-argument relaunch calls

# v0.1.4 (07/05/2026)

- Add serialized `SettingsVersion`, `CurrentSettingsVersion`, and `MigrateSettings(...)` for settings schema migrations.
- Add `ValidateSettings(...)` and `SettingsValidationContext` for load-time validation and repair.
- Add `SuspendAutoSave(...)` and `BatchUpdate(...)` to batch changes without scheduling repeated saves.
- Add `ApplicationInstanceGuard` for named-mutex single-instance detection.
- Add `SafeFile`, `ApplicationBackup`, `SupportBundleExporter`, `ApplicationRetention`, and `OnboardingStateFile`
  utilities.
- Use `SafeFile` for `RootSettingsFile<T>` persistence writes.
- Add in-memory `RootSettingsFile<T>.SaveCount`, ignored in JSON.
- Keep `ApplicationInstanceGuard` as a direct named-mutex wrapper; dispose must run on the same thread that acquired the
  guard.
- Fix duplicate `RootCollectionFile<T,TO>` item instances so item change tracking remains subscribed until the last
  reference is removed.
- Fix stale `ApplicationKit.CrashReportIndex` after replacing application args with missing or invalid crash-report
  values.
- Fix crash report retention to suppress repeated autosaves and persist once after removals.
- Fix support bundle exports created under logs/configs so the bundle does not include its own destination or temp file.

# v0.1.3 (03/05/2026)

- Convert several virtual/static settings members into instance-level properties and initializers to allow per-instance
  configuration.
- Fix `CrashReportsFile` default directory to be under `ApplicationKit.LogsPath` instead of `ApplicationKit.ConfigsPath`

# v0.1.2 (03/05/2026)

- Allow to change profile configuration directories

# v0.1.1 (03/05/2026)

- Add `ObservableCollections` package to support thread-safe observable collections
- Use `Microsoft.Extensions.Logging.Abstractions` to avoid unnecessary logging implementation dependencies
- Fix: `UnhandledExceptions.HandleUnhandledException` could terminate the process even when the exception matched the
  ignore list (`return` only exited the inner `try` block).
- Fix: `RootCollectionFile<T,TO>` leaked per-item `PropertyChanged` subscriptions when trimming with
  `TrackItemsWithChangeNotification = true`.
- Fix: settings files are now written atomically (temp file + flush + `File.Move` overwrite) to prevent corruption on
  crash mid-write.
- Fix: `WaitForDebouncedSaveAsync` no longer returns `true` while a save is mid-write; replaces 100 ms polling with a
  `TaskCompletionSource`-signaled wait.
- Fix: `ApplicationKit.ApplicationArgs` setter now throws `ArgumentNullException` instead of NRE when set to `null`.
- Fix: corrupt settings files are renamed to `<file>.corrupt-<timestampUtc>` before fresh-instance fallback (no silent
  data loss).
- Fix: orphan `RootCollectionFile<T,TO>` instance disposed when JSON deserialization throws partway through
  `LoadOrCreate`.
- Fix: `RootCollectionFile<T,TO>.Dispose` always unsubscribes per-item `PropertyChanged`, even if
  `TrackItemsWithChangeNotification` was toggled false at runtime.
- Perf: `UnhandledExceptions.CanIgnoreException` walks each set once and avoids LINQ delegate allocations.
- Perf: `CrashReportsFile.GetActual` short-circuits with `FirstOrDefault` instead of `LastOrDefault`.
- Internal: `RootSettingsFile<T>.CanSave` now uses a `volatile` backing field for cross-thread visibility.
- Docs: `RootCollectionFile<T,TO>.ItemsView` now documents that the synchronization context is captured at construction.
- Improve `SubSettings`, `RootSettingsFile` and `RootCollectionFile`
  - Add `HasUnsavedChanges` property to track unsaved changes
  - Add `SubSettingsCollection` property to update and keep track of sub-settings
  - Add `TrackItemsWithChangeNotification` property to track item property changes in collections

# v0.1.0 (02/05/2026)

- Initial release
