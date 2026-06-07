# v0.2.1 (07/06/2026)

- Add `CrashReport.GcTotalMemory`, `GcTotalAllocatedBytes`, and `GcCollectionCounts` to capture managed heap, lifetime
  allocations, and per-generation GC counts at crash time.
- Add `CrashReport.CustomData` (`IReadOnlyDictionary<string, object?>`) for application-specific key-value data,
  appended to the formatted report.
- Add `StageKitExceptionEventArgs` carrying `Category`, `IsIgnored`, and `CustomData`, with a `ToCrashReport()` helper
  and conversion constructors from `UnhandledExceptionEventArgs`/`UnobservedTaskExceptionEventArgs`.
- Add `UnhandledExceptions.ExceptionThrown` event, raised for every caught unhandled exception (fatal or ignored).
- Add `UnhandledExceptions.HandleSafeException(...)` to log a non-fatal exception at a configurable `LogLevel`.
- Add `ExceptionTraversalType` and a traversal-type parameter to `UnhandledExceptions.TraverseExceptions(...)` for
  selecting complete exception-tree or direct inner-chain traversal.
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
