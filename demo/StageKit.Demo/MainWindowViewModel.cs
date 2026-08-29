using System.Diagnostics;
using System.ComponentModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StageKit.Runtime;
using StageKit.Updatum;

namespace StageKit.Demo;

public sealed record RuntimeValue(string Label, string Value);

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly UpdatumManager _updater = DemoUpdateManager.Create();
    private CancellationTokenSource? _updateCancellation;
    private UpdatumDownloadedAsset? _downloadedAsset;
    private readonly DemoCrashPresentation? _startupCrashReport;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready to explore StageKit.";

    [ObservableProperty]
    public partial string RuntimeReport { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewRecentDocument { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastArtifactPath { get; set; } = "No artifact created in this session.";

    [ObservableProperty]
    public partial string UpdaterStatus { get; set; } = "Not checked yet.";

    [ObservableProperty]
    public partial string ReleaseSummary { get; set; } = "No release selected.";

    [ObservableProperty]
    public partial string CompatibleAssetName { get; set; } = "No compatible asset selected.";

    [ObservableProperty]
    public partial string ChangelogText { get; set; } = "Check for updates to load release notes.";

    [ObservableProperty]
    public partial string DownloadProgress { get; set; } = "0.00 MB / 0.00 MB (0%)";

    [ObservableProperty]
    public partial double DownloadPercentage { get; set; }

    [ObservableProperty]
    public partial bool CanDownloadUpdate { get; set; }

    [ObservableProperty]
    public partial bool HasDownloadedAsset { get; set; }

    [ObservableProperty]
    public partial bool AutoInstallUpdates { get; set; }

    [ObservableProperty]
    public partial bool CanInstallUpdate { get; set; }

    [ObservableProperty]
    public partial int SelectedFeatureIndex { get; set; }

    public MainWindowViewModel()
    {
        _startupCrashReport = DemoCrashPresentation.Create(
            ApplicationKit.HasCrashReportFlag,
            ApplicationKit.CrashReportIndex,
            ApplicationKit.CrashReport?.ToString());
        SelectedFeatureIndex = _startupCrashReport is null ? 0 : 2;
        Settings.PropertyChanged += SettingsOnPropertyChanged;
        _updater.PropertyChanged += UpdaterOnPropertyChanged;
        ApplyTheme(Settings.Theme);
        RefreshRuntime();
        RefreshStageKitState();
    }

    public string WindowTitle => $"StageKit Demo v{EntryApplication.AssemblyVersionString} ({EntryApplication.GenericRuntimeIdentifier})";

    public AppSettings Settings => AppSettings.Instance;

    public RecentDocuments RecentDocuments => RecentDocuments.Instance;

    public IEnumerable<string> RecentItems => RecentDocuments.ItemsView;

    public IReadOnlyList<DemoFeature> Features => DemoFeatureCatalog.All;

    public IReadOnlyList<RuntimeValue> RuntimeValues { get; private set; } = [];

    public string ProfilePath => ApplicationKit.ProfilePath;

    public string SettingsDirectoryPath => ApplicationKit.ConfigsPath;

    public string StorageDirectoryPath => ApplicationKit.ProfilePath;

    public IReadOnlyList<string> ThemeOptions => DemoThemeOptions.Values;

    public bool HasStartupCrashReport => _startupCrashReport is not null;

    public string StartupCrashReportTitle => _startupCrashReport is null
        ? "No crash report was supplied at startup."
        : $"Recovered crash report #{_startupCrashReport.ReportId}";

    public string StartupCrashReportText => _startupCrashReport?.ReportText
        ?? "Use the fatal crash action below to exercise StageKit's persist, relaunch, and report-loading flow.";

    public string SettingsFileStatus =>
        $"{Settings.FileName} · Saves: {Settings.SaveCount} · Unsaved: {Settings.HasUnsavedChanges}";

    public string OnboardingSummary => OnboardingStateFile.Instance.ToString();

    public string CrashReportSummary =>
        $"Enabled: {CrashReportsFile.IsEnabled} · Stored reports: {CrashReportsFile.Instance.Count}";

    public string UpdaterRepository => $"{_updater.Owner}/{_updater.Repository}";

    [RelayCommand]
    private void RefreshRuntime()
    {
        RuntimeValues =
        [
            new RuntimeValue("Framework", Environment.Version.ToString()),
            new RuntimeValue("Runtime", EntryApplication.GenericRuntimeIdentifier),
            new RuntimeValue("Packaging", EntryApplication.PackagingType.ToString()),
            new RuntimeValue("Session", ApplicationKit.SessionId.ToString())
        ];
        RuntimeReport = RuntimeDiagnostics.GetReport();
        OnPropertyChanged(nameof(RuntimeValues));
        OnPropertyChanged(nameof(ProfilePath));
        StatusMessage = "Runtime diagnostics refreshed.";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        Settings.Save();
        RefreshStageKitState();
        StatusMessage = $"Settings saved atomically to {Settings.FilePath}";
    }

    [RelayCommand]
    private void OpenSettingsDirectory()
    {
        OpenDirectory(ApplicationKit.ConfigsPath, "settings directory", "[StageKit.Demo.SettingsDirectory]");
    }

    [RelayCommand]
    private void OpenProfileDirectory()
    {
        OpenDirectory(ApplicationKit.ProfilePath, "profile directory", "[StageKit.Demo.ProfileDirectory]");
    }

    private void OpenDirectory(string path, string description, string category)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            StatusMessage = $"Opened {description}: {path}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not open the {description}: {exception.Message}";
            UnhandledExceptions.HandleSafeException(exception, category);
        }
    }

    [RelayCommand]
    private async Task WaitForAutoSaveAsync()
    {
        var saved = await Settings.WaitForDebouncedSaveAsync(TimeSpan.FromSeconds(5));
        RefreshStageKitState();
        StatusMessage = saved
            ? "Autosave completed."
            : "No completed autosave was observed within five seconds.";
    }

    [RelayCommand]
    private void AddRecentDocument()
    {
        var document = NewRecentDocument.Trim();
        if (document.Length == 0)
        {
            StatusMessage = "Enter a document name first.";
            return;
        }

        RecentDocuments.Insert(0, document);
        RecentDocuments.TrimCollectionWhenExceeding = Math.Max(1, Settings.General.MaxRecentDocuments);
        NewRecentDocument = string.Empty;
        StatusMessage = $"Added '{document}' to the autosaved recent-document collection.";
    }

    [RelayCommand]
    private void ClearRecentDocuments()
    {
        RecentDocuments.Clear();
        StatusMessage = "Recent documents cleared; autosave is pending.";
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        await RunArtifactOperationAsync(
            "profile backup",
            () => ApplicationBackup.CreateAsync());
    }

    [RelayCommand]
    private async Task ExportSupportBundleAsync()
    {
        await RunArtifactOperationAsync(
            "support bundle",
            () => SupportBundleExporter.ExportAsync(new SupportBundleOptions
            {
                IncludeConfigs = true,
                IncludeLogs = true,
                IncludeCrashReports = true,
                Notes = "Created from the StageKit Avalonia demo."
            }));
    }

    [RelayCommand]
    private void ApplyRetention()
    {
        ApplicationRetention.LogRetentionPolicy.MaxAge = TimeSpan.FromDays(14);
        ApplicationRetention.LogRetentionPolicy.MaxFiles = 20;
        var logResult = ApplicationRetention.ApplyLogRetention();
        var crashReportCount = ApplicationRetention.ApplyCrashReportRetention(20, TimeSpan.FromDays(30));
        StatusMessage =
            $"Retention removed {logResult.DeletedCount} log file(s) and {crashReportCount} crash report(s).";
        RefreshStageKitState();
    }

    [RelayCommand]
    private void CompleteOnboarding()
    {
        OnboardingStateFile.Instance.CompleteOnboarding(EntryApplication.AssemblyVersionString);
        StatusMessage = "Onboarding marked complete.";
        RefreshStageKitState();
    }

    [RelayCommand]
    private void ResetOnboarding()
    {
        OnboardingStateFile.Instance.ResetOnboarding();
        StatusMessage = "Onboarding reset; the next launch is a first-run experience.";
        RefreshStageKitState();
    }

    [RelayCommand]
    private void RecordSafeException()
    {
        UnhandledExceptions.HandleSafeException(
            new InvalidOperationException("Demonstration exception created by the user."),
            "[StageKit.Demo]");
        StatusMessage = "A handled demonstration exception was sent through StageKit logging.";
        RefreshStageKitState();
    }

    [RelayCommand]
    private void ThrowFatalException()
    {
        Settings.EnableCrashReporting = true;
        CrashReportsFile.IsEnabled = true;
        throw new InvalidOperationException(
            "Intentional StageKit demo crash. The application should persist this report and relaunch.");
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        var cancellation = ReplaceUpdateCancellation();
        try
        {
            UpdaterStatus = $"Checking {_updater.Owner}/{_updater.Repository}…";
            var updateFound = await _updater.CheckForUpdatesAsync(cancellation.Token);
            if (!updateFound || _updater.LatestRelease is null)
            {
                ReleaseSummary = "The current build is up to date, or no compatible newer release was found.";
                CompatibleAssetName = "No compatible asset selected.";
                ChangelogText = "No newer release notes are available.";
                CanDownloadUpdate = false;
                UpdaterStatus = "Update check completed.";
                return;
            }

            var release = _updater.LatestRelease;
            var asset = _updater.GetCompatibleReleaseAsset(release);
            ReleaseSummary = $"{release.Name ?? release.TagName} · {release.TagName}";
            CompatibleAssetName = asset?.Name ?? "No compatible asset for this runtime.";
            ChangelogText = _updater.GetChangelog() ?? "No release notes provided.";
            CanDownloadUpdate = asset is not null;
            UpdaterStatus = asset is null
                ? "A release was found, but it has no compatible asset."
                : "A verified download can now be started.";
        }
        catch (OperationCanceledException)
        {
            UpdaterStatus = "Update operation cancelled.";
        }
        catch (Exception exception)
        {
            UpdaterStatus = $"Update check failed: {exception.Message}";
            UnhandledExceptions.HandleSafeException(exception, "[StageKit.Demo.Updatum]");
        }
        finally
        {
            CompleteUpdateCancellation(cancellation);
        }
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (!CanDownloadUpdate)
        {
            UpdaterStatus = "Check for a compatible update first.";
            return;
        }

        var cancellation = ReplaceUpdateCancellation();
        try
        {
            _downloadedAsset?.SafeDeleteFile();
            _downloadedAsset = null;
            CanInstallUpdate = false;
            _downloadedAsset = await _updater.DownloadUpdateAsync(cancellation.Token);
            HasDownloadedAsset = _downloadedAsset is not null;
            UpdaterStatus = _downloadedAsset is null
                ? "The update was not downloaded."
                : $"Downloaded and verified: {_downloadedAsset.FilePath}";
            CanInstallUpdate = _downloadedAsset is not null;
            if (AutoInstallUpdates && _downloadedAsset is not null)
            {
                await InstallDownloadedUpdateCoreAsync(_downloadedAsset, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            UpdaterStatus = "Update download cancelled.";
        }
        catch (Exception exception)
        {
            UpdaterStatus = $"Update download failed: {exception.Message}";
            UnhandledExceptions.HandleSafeException(exception, "[StageKit.Demo.Updatum]");
        }
        finally
        {
            CompleteUpdateCancellation(cancellation);
        }
    }

    [RelayCommand]
    private async Task InstallDownloadedUpdateAsync()
    {
        if (_downloadedAsset is null)
        {
            UpdaterStatus = "Download and verify an update before installing it.";
            return;
        }

        var cancellation = ReplaceUpdateCancellation();
        try
        {
            await InstallDownloadedUpdateCoreAsync(_downloadedAsset, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            UpdaterStatus = "Update installation cancelled.";
        }
        catch (Exception exception)
        {
            UpdaterStatus = $"Update installation failed: {exception.Message}";
            UnhandledExceptions.HandleSafeException(exception, "[StageKit.Demo.Updatum]");
        }
        finally
        {
            CompleteUpdateCancellation(cancellation);
        }
    }

    private async Task InstallDownloadedUpdateCoreAsync(
        UpdatumDownloadedAsset downloadedAsset,
        CancellationToken cancellationToken)
    {
        UpdaterStatus = "Installing the verified update… The app will restart when the installer is ready.";
        var started = await _updater.InstallUpdateAsync(
            downloadedAsset,
            forceTerminate: true,
            runArguments: null,
            cancellationToken).ConfigureAwait(false);
        if (!started)
        {
            UpdaterStatus = "The verified asset could not be installed on this platform.";
        }
    }

    [RelayCommand]
    private void CancelUpdateOperation()
    {
        _updateCancellation?.Cancel();
    }

    [RelayCommand]
    private void DeleteDownloadedAsset()
    {
        _downloadedAsset?.SafeDeleteFile();
        _downloadedAsset = null;
        HasDownloadedAsset = false;
        CanInstallUpdate = false;
        UpdaterStatus = "Downloaded update asset removed from the temporary workspace.";
    }

    private async Task RunArtifactOperationAsync(string artifactName, Func<Task<string>> operation)
    {
        try
        {
            StatusMessage = $"Creating {artifactName}…";
            LastArtifactPath = await operation();
            StatusMessage = $"Created {artifactName}.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not create {artifactName}: {exception.Message}";
            UnhandledExceptions.HandleSafeException(exception, "[StageKit.Demo.Storage]");
        }
    }

    private CancellationTokenSource ReplaceUpdateCancellation()
    {
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updateCancellation = new CancellationTokenSource();
        return _updateCancellation;
    }

    private void CompleteUpdateCancellation(CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(_updateCancellation, cancellation)) return;

        _updateCancellation.Dispose();
        _updateCancellation = null;
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.EnableCrashReporting))
            CrashReportsFile.IsEnabled = Settings.EnableCrashReporting;
        if (e.PropertyName == nameof(AppSettings.Theme)) ApplyTheme(Settings.Theme);

        OnPropertyChanged(nameof(SettingsFileStatus));
        OnPropertyChanged(nameof(CrashReportSummary));
    }

    private static void ApplyTheme(string? theme)
    {
        if (Application.Current is not { } application) return;

        application.RequestedThemeVariant = DemoThemeOptions.Resolve(theme);
    }

    private void UpdaterOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdatumManager.State))
            UpdaterStatus = $"Updater state: {_updater.State}";

        if (e.PropertyName is nameof(UpdatumManager.DownloadedPercentage)
            or nameof(UpdatumManager.DownloadedMegabytes)
            or nameof(UpdatumManager.DownloadSizeMegabytes))
        {
            DownloadPercentage = _updater.DownloadedPercentage;
            DownloadProgress = DemoFormatting.FormatDownloadProgress(
                _updater.DownloadedMegabytes,
                _updater.DownloadSizeMegabytes,
                _updater.DownloadedPercentage);
        }
    }

    private void RefreshStageKitState()
    {
        OnPropertyChanged(nameof(SettingsFileStatus));
        OnPropertyChanged(nameof(OnboardingSummary));
        OnPropertyChanged(nameof(CrashReportSummary));
    }

    public void Dispose()
    {
        Settings.PropertyChanged -= SettingsOnPropertyChanged;
        _updater.PropertyChanged -= UpdaterOnPropertyChanged;
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _downloadedAsset?.SafeDeleteFile();
        _updater.Dispose();
    }
}
