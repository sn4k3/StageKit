# v0.3.9 (/09/2026)

- Enforce a configurable macOS minimum runtime version in the generated installation script, defaulting to macOS 13.0
- Emit current AppStream developer metadata, including a configurable developer ID, for Linux application bundles
- Normalize AppStream summaries by removing trailing periods
- Add option to stop configured running application instances before installation
- Add `InstallerPathRegistration` modes for optional or enforced WiX PATH registration, with user/system scope handling,
  upgrade preference restoration, uninstall cleanup, and checked or unchecked user defaults
- Isolate WiX intermediate outputs by installer name to prevent incremental builds from reusing another version's MSI
- Keep WiX shortcut registry state in the selected user or machine installation scope
- Stop the WiX installer from overriding a chosen installation directory with the remembered one, which silently
  redirected wizard and `INSTALLFOLDER=` command-line choices back to the previous directory
- Restore the WiX shortcut preferences on silent and basic-UI installations, which never run the wizard and so
  recreated shortcuts the user had removed
- Scope the WiX PATH entry and remembered directory by the installation context Windows Installer resolved
  (`ALLUSERS`) rather than by the wizard's `INSTALLSCOPE`, so a per-user installation writes the user PATH instead of
  failing with an access denied error while trying to write the system PATH
- Delete the WiX installer registry key on uninstall so nothing is left behind
- Keep the WiX "Start program after install" checkbox working after a major upgrade instead of showing it inert, and
  check it by default
- Default silent per-user WiX installations to the user programs directory so unprivileged installs succeed without
  requiring elevation or failing against Program Files
- Forward `Company`, `Copyright`, `RepositoryUrl`, and `WindowsIconFile` from the Windows publish pipeline to the WiX
  installer build
- Disable the WiX installation scope selector during maintenance or while a previous installation is detected, since
  Windows Installer keeps a product in the context it was first installed in
- Uninstall directly from the WiX start menu shortcut, which now carries the application icon, instead of opening
  maintenance mode that `ARPNOMODIFY` disables
- Mark the WiX shortcut components transitive so their conditions are re-evaluated on reinstall
- Record the copyright in the WiX package summary information
- Authenticode-sign every executable and library in the WiX payload instead of only the entry executable, through an
  `InstallerPayloadToSign` item that can be redefined to narrow the set
- Honour a preset `InstallerPathRegistration` in the WiX installer project instead of always overwriting it
- Add the StageKit demo icon to its window and taskbar entry

# v0.3.8 (11/09/2026)

- Let users choose between per-user and per-machine WiX installation scopes
- Let installer developers enforce `perUser` or `perMachine` through the WiX `InstallerScope` property while defaulting
  to user-selectable scope
- Add optional SHA-256 Authenticode signing for the staged Windows executable and final MSI
- Prevent x64 installers from running under ARM64 emulation when a native ARM64 installer is available
- Allow older and same-version MSI packages to replace the installed version
- Add a uninstall software shortcut to start menu in the Wix template
- Let users choose the WiX installation directory and restore the last directory on later installs, upgrades, and
  reinstalls
- Record the installed MSI product code, software version, architecture, and executable path alongside the remembered
  installation directory, while removing installed-product metadata during uninstall
- Move the optional "Start program after install" checkbox to the completion page

# v0.3.7 (11/09/2026)

- Remove `<TargetFramework>net10.0</TargetFramework>` from WixInstaller template
- Add `UnixSystem.SetUnixPermissions(...)` to set the permissions of a file or directory
- Add `UnixSystem.ChangeUnixPermissions(...)` to change the current permissions of a file or directory
- Add `StageKitBuild.UnixFilePermissions` to apply configured `chmod` modes to validated files beneath each Unix-like
  runtime's publish directory after publishing
- Improve the `StageKitBuild.SoftwareName` property to guess from more properties

# v0.3.6 (10/09/2026)

- Add the `EntryApplication.CurrentProcess` cached property to expose the current process
- Hardens Fallout main-project detection by ignoring missing or non-boolean `FalloutMainProject` values
- Updates host GPU reporting so `GraphicsCardName` returns all detected adapter names
- Reshapes the Linux package-manager command helper into the newer extension-property style.

# v0.3.5 (10/09/2026)

- Add the `GenerateWindowsWixInstaller` Fallout target, which scaffolds `builds/<SoftwareName>.WixInstaller` with the
  WiX project, `Package.wxs`, `Strings.en-us.wxl`, a readme, a placeholder `License.rtf`, and generated placeholder
  banner and dialog artwork at the sizes WiX expects. Upgrade codes are generated once per platform, and the target
  fails instead of overwriting an existing installer project so those codes and any custom authoring survive
- Replace existing same-version Windows installations when installing a rebuilt MSI instead of registering another
  product.
- Allow custom Windows installer welcome artwork and header logos, including the installation-options page.
- Add Windows installer checkboxes for Start menu and Desktop shortcuts (enabled by default and restored from the
  registry on rerun/upgrade) and starting the app after installation (disabled by default).
- Prefer RPM assets on AlmaLinux in generated installers, and retain the application-name casing for installed AppImage
  and .NET single-file names
- Stop Linux `speaker-test` tones at the requested duration and use macOS `afplay` for an audible system sound

# v0.3.4 (09/09/2026)

- Allow set AppImage squashfs compression in Fallout to customize or override compression
- Decouple primary directory publish from `DotNetSingleFile` packaging so that directory and portable archive publish
  retains all runtime assemblies and native dependencies
- Add the `PublishReadyToRun` build parameter (`--publish-ready-to-run`), defaulting to `false`, to make ReadyToRun
  compilation opt-in
- Add the `PublishTrimmed` build parameter (`--publish-trimmed` and `Trimmed` property alias), defaulting to `false`, to
  make assembly trimming opt-in across publish and restore operations
- Let APT's `_apt` user read downloaded DEB files without falling back to an unsandboxed root download, and print the
  command that starts applications installed through native Linux packages

# v0.3.3 (08/09/2026)

- Add `ShellScriptFile`, a `TextWriter` that builds and executes Windows batch or Unix shell scripts with optional
  elevation, cancellation, captured process output, argument lists, platform-conditional writes, comments, and
  environment variables. `CreateTemporary(...)` and the pathless constructor assign a unique temporary path and delete
  it on disposal by default, while an explicit path keeps the script; content is buffered and written atomically by
  `Flush`, by `Execute`, and on disposal.
  `WriteLines`, `WriteLinesAsync`, `WriteLinesIf`, `WriteLinesIfWindows`/`MacOS`/`Linux`/`Unix`, and `WriteComments`
  write several lines at once, taking `params` or an `IEnumerable<string?>`
- Add raw and structured script-argument support to `ProcessHelper.CreateShellScriptProcessStartInfo`, plus
  `StartShellScript` and `StartShellScriptAsync` overloads matching the process-start helpers
- Move process lifetime metadata from `ApplicationKit.StartingTimestamp`, `RuntimeElapsed`, and `SessionId` to
  `EntryApplication.ProcessStartingTimestamp`, `ProcessUptime`, and `ProcessSessionId`; the former APIs have been
  removed
- Add structured process snapshots and configurable runtime reports to `RuntimeDiagnostics`, including process, GC,
  thread-pool, runtime-feature, culture, and time-zone details plus reusable custom report formatting
- Add portable processor and multi-adapter graphics information to `HostSystem`, with native or structured detection,
  Windows filtering for indirect USB and software display drivers, bounded external queries, retry-after-failure
  behavior, and compatibility for the original `GraphicCardName` spelling
- Add cached system manufacturer and model detection plus a portable `HostSystem.SystemUptime` measurement
- Add `HostSystem.IsNetworkAvailable()` for a fast, cross-platform local network-availability check and
  `IsInternetAvailableAsync()` for an active, timeout-bound internet connectivity probe with captive-portal detection
- Add cached `HostSystem.OperatingSystemName` and `OperatingSystemNameWithArch` display values, including Mac Catalyst,
  WASI, and correctly styled tvOS and watchOS names
- Add immutable, platform-neutral host memory snapshots with native Windows/macOS queries, span-based Linux parsing,
  readable physical-memory properties, and explicit failure detection
- Show live host memory totals, availability, usage, and load in the Avalonia demo with automatic two-second refreshes
- Add `StringExtensions.InsertCharBetweenCamelCase` to `StageKit.Primitives` for splitting camelCase/PascalCase (and,
  optionally, digit) transitions with a configurable separator character
- Add `HostSystem.Beep` and `HostSystem.BeepAsync` to `StageKit.Primitives`. Both report success as a `bool` instead of
  throwing, clamp the frequency to the 37 Hz floor `Console.Beep` requires, format the Linux `sleep` interval with the
  invariant culture, probe for `speaker-test` before using it, and fall back to the macOS system alert sound
- Add a host beep section to the Avalonia demo with frequency and duration editors, single and looping playback through
  `HostSystem.BeepAsync`, and a stop button that cancels the loop
- Add `LinuxRpm` and `DotNetSingleFile` to `StageKit` releases
- Fix cross-architecture RPM publishing by passing the target architecture to `rpmbuild`, fail immediately when a
  packaging command exits unsuccessfully, and report successful AppImage progress as regular output
- Run Linux and macOS release jobs on matching native x64 or ARM64 GitHub-hosted runners
- Retry transient `hdiutil create` resource-busy failures when producing macOS DMG packages
- Log shell tool output at information level regardless of its output stream, skip unnecessary Arch dependency checks,
  and include complete Snap store metadata; the StageKit demo Snap now stages its fontconfig runtime dependency
- Set `CreateNoWindow = true` on `ProcessHelper` by default to avoid a console window on Linux
- Add `ProcessHelper.StartProcessWithShellExecute` and `StartProcessWithShellExecuteAsync` overloads for targets that
  must be opened through the operating system shell

# v0.3.2 (05/09/2026)

- Fix macOS ZIP, PKG, and DMG auto-updates leaving the application closed. ZIP updates now replace the extracted `.app`
  instead of its parent directory, and relaunch uses a new Launch Services instance with startup verification and a
  diagnostic log in the system temporary directory.
- Fall back to `wget` in Bash build and generated installation scripts when `curl` is unavailable.
- Fix arch installer warnings from an unnecessary `awk` regex escape and accept `/help` and `/?` aliases.
- Add Bash `--portable [PATH]` extraction into an application-named directory and generate a package-aware Bash
  uninstaller that probes all supported Linux and macOS formats.
- Generate a Windows PowerShell uninstaller that probes an exact configured WinGet package, exact registered installer
  display names, and the known per-user Portable/single-file directory and `PATH` entries.

# v0.3.1 (04/09/2026)

- Add Fallout's `GenerateInstallScript` target, which creates GitHub Releases Bash and Windows PowerShell installers
  from the selected `PackagingTypes`, exposes command help and explicit downgrade/version selection, prioritizes native
  installers over generic bundles, keeps Portable last, lists all published release versions or their release-note
  changelogs (limited to 20 releases by default), reports clean PowerShell release-resolution errors, and falls back
  when an asset or package tool is unavailable. Windows scripts can optionally try an exact WinGet package ID first and
  fall back to GitHub release assets when WinGet is unavailable or installation fails.
- Fix macOS PKG and DMG auto-updates leaving the application closed by deferring relaunch to a non-elevated helper that
  waits for the old process to exit. Set the StageKit demo's Avalonia application name so its macOS menu uses
  `StageKit`.
- Fix macOS DMG creation being killed with exit code 137 on GitHub-hosted runners. The image is built from the
  application bundle alone again, without the `/Applications` drag-and-drop symlink, because `hdiutil` follows that
  symlink while scanning the source tree and walks the whole host `/Applications` directory. PKG installers continue to
  install their application directly in `/Applications`. Native packages are still created before the application ZIP.
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
