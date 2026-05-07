# v0.1.4 (07/05/2026)
- Add serialized `SettingsVersion`, `CurrentSettingsVersion`, and `MigrateSettings(...)` for settings schema migrations.
- Add `ValidateSettings(...)` and `SettingsValidationContext` for load-time validation and repair.
- Add `SuspendAutoSave(...)` and `BatchUpdate(...)` to batch changes without scheduling repeated saves.
- Add `ApplicationInstanceGuard` for named-mutex single-instance detection.
- Add `SafeFile`, `ApplicationBackup`, `SupportBundleExporter`, `ApplicationRetention`, and `OnboardingStateFile` utilities.
- Use `SafeFile` for `RootSettingsFile<T>` persistence writes.
- Add in-memory `RootSettingsFile<T>.SaveCount`, ignored in JSON.
- Keep `ApplicationInstanceGuard` as a direct named-mutex wrapper; dispose must run on the same thread that acquired the guard.
- Fix duplicate `RootCollectionFile<T,TO>` item instances so item change tracking remains subscribed until the last reference is removed.
- Fix stale `ApplicationKit.CrashReportIndex` after replacing application args with missing or invalid crash-report values.
- Fix crash report retention to suppress repeated autosaves and persist once after removals.
- Fix support bundle exports created under logs/configs so the bundle does not include its own destination or temp file.

# v0.1.3 (03/05/2026)
- Convert several virtual/static settings members into instance-level properties and initializers to allow per-instance configuration. 
- Fix `CrashReportsFile` default directory to be under `ApplicationKit.LogsPath` instead of `ApplicationKit.ConfigsPath`

# v0.1.2 (03/05/2026)
- Allow to change profile configuration directories

# v0.1.1 (03/05/2026)
- Add `ObservableCollections` package to support thread-safe observable collections
- Use `Microsoft.Extensions.Logging.Abstractions` to avoid unnecessary logging implementation dependencies
- Fix: `UnhandledExceptions.HandleUnhandledException` could terminate the process even when the exception matched the ignore list (`return` only exited the inner `try` block).
- Fix: `RootCollectionFile<T,TO>` leaked per-item `PropertyChanged` subscriptions when trimming with `TrackItemsWithChangeNotification = true`.
- Fix: settings files are now written atomically (temp file + flush + `File.Move` overwrite) to prevent corruption on crash mid-write.
- Fix: `WaitForDebouncedSaveAsync` no longer returns `true` while a save is mid-write; replaces 100 ms polling with a `TaskCompletionSource`-signaled wait.
- Fix: `ApplicationKit.ApplicationArgs` setter now throws `ArgumentNullException` instead of NRE when set to `null`.
- Fix: corrupt settings files are renamed to `<file>.corrupt-<timestampUtc>` before fresh-instance fallback (no silent data loss).
- Fix: orphan `RootCollectionFile<T,TO>` instance disposed when JSON deserialization throws partway through `LoadOrCreate`.
- Fix: `RootCollectionFile<T,TO>.Dispose` always unsubscribes per-item `PropertyChanged`, even if `TrackItemsWithChangeNotification` was toggled false at runtime.
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
