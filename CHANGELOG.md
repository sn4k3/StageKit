# v0.1.1 (03/05/2026)
- Add `ObservableCollections` package to support thread-safe observable collections
- Use `Microsoft.Extensions.Logging.Abstractions` instead of `Microsoft.Extensions.Logging` to avoid unnecessary dependencies
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
