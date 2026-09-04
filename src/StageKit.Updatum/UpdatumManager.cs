using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using Octokit;
using StageKit.Primitives;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;
using StageKit.Runtime;
using StageKit.Runtime.System;
using StageKit.Updatum.Extensions;
using Timer = System.Timers.Timer;

namespace StageKit.Updatum;

/// <summary>
/// Represents the Updatum class.
/// </summary>
public partial class UpdatumManager : DisposableObject, INotifyPropertyChanged
{
    #region Timer

    /// <summary>
    /// Starts the auto-update check timer.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="ApiException"/>
    private async void AutoUpdateCheckTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            await CheckForUpdatesAsync(_disposeCancellationSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsDisposed)
        {
            // Disposal cancels any timer-triggered operation.
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    #endregion

    #region Dispose

    /// <inheritdoc />
    protected override void DisposeManaged()
    {
        _disposeCancellationSource.Cancel();
        if (_autoUpdateCheckTimer is not null)
        {
            _autoUpdateCheckTimer.Stop();
            _autoUpdateCheckTimer.Elapsed -= AutoUpdateCheckTimerOnElapsed;
            _autoUpdateCheckTimer.Dispose();
        }

        _disposeCancellationSource.Dispose();
        _propertyChanged = null;
        CheckForUpdateCompleted = null;
        UpdateFound = null;
        DownloadCompleted = null;
        InstallUpdateCompleted = null;
    }

    #endregion

    #region Events

    /// <summary>
    ///     Multicast event for property change notifications.
    /// </summary>
    private PropertyChangedEventHandler? _propertyChanged;

    /// <summary>
    /// Occurs when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            _propertyChanged -= value;
            _propertyChanged += value;
        }
        remove => _propertyChanged -= value;
    }

    /// <summary>
    /// Occurs when the check for update is completed.
    /// </summary>
    public event EventHandler? CheckForUpdateCompleted;

    /// <summary>
    /// Occurs when an update is found.
    /// </summary>
    public event EventHandler? UpdateFound;

    /// <summary>
    /// Occurs when the download is completed.
    /// </summary>
    public event EventHandler<UpdatumDownloadedAsset>? DownloadCompleted;

    /// <summary>
    /// Occurs when the second stage of auto installation is completed.<br/>
    /// This is after unpacking and script creation but before actual install/replace and the killing of the current process.<br/>
    /// This event is useful to perform any custom action before the process is killed, like saving the current state or settings.
    /// </summary>
    public event EventHandler<UpdatumDownloadedAsset>? InstallUpdateCompleted;

    #endregion

    #region Constants

    /// <summary>
    /// The URL of the GitHub homepage.
    /// </summary>
    private const string GitHubUrl = "https://github.com";

    /// <summary>
    /// Token to prevent the app from rerunning after an upgrade.
    /// </summary>
    public const string NoRunAfterUpgradeToken = "$NORUN!";

    /// <summary>
    /// Default buffer size for the download stream.
    /// </summary>
    private const int DefaultBufferSize = 8192;

    /// <summary>
    /// GitHub REST API media type used to retrieve release asset metadata.
    /// </summary>
    private const string GitHubApiMediaType = "application/vnd.github+json";

    /// <summary>
    /// Prefix used by GitHub for SHA-256 release asset digests.
    /// </summary>
    private const string GitHubSha256DigestPrefix = "sha256:";

    private const int PackageInstallTimeoutMilliseconds = 15 * 60 * 1000;

    /// <summary>
    /// Default file extension for Windows installers.
    /// </summary>
    private static readonly string[] WindowsInstallerFileExtensions = [".msi", ".exe"];

    internal enum FlatpakInstallationScope
    {
        User,
        System
    }

    internal readonly record struct LinuxPackageInstallCommand(
        string PackageType,
        string Executable,
        string[] Arguments,
        bool RequiresElevation);

    #endregion

    #region Static Properties

    /// <summary>
    /// Gets the current version of this library (<see cref="UpdatumManager"/>).
    /// </summary>
    public static Version LibraryVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    private static Version EntryAssemblyVersion => EntryApplication.AssemblyVersion ?? new Version(0, 0, 0, 0);

    [GeneratedRegex(@"\d+\.\d+(?:\.\d+){0,2}(?:[-_](?:dev|alpha|beta|preview|rc|nightly|canary)\d*)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExtractVersionRegex();

    [GeneratedRegex(@"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    /// <summary>
    /// The default GitHub API options, defaults to save tokens and maximize output up to 30 releases.
    /// </summary>
    /// <remarks>You can use up to 100 releases in PageSize.</remarks>
    public static readonly ApiOptions GitHubApiOptions = new()
    {
        PageCount = 1,
        PageSize = 30
    };

    /// <summary>
    /// Gets the HTTP client used to access the GitHub API.
    /// </summary>
    public static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders =
        {
            UserAgent =
            {
                new ProductInfoHeaderValue(EntryApplication.AssemblyName ?? nameof(UpdatumManager),
                    EntryAssemblyVersion.ToString())
            }
        }
    };

    #endregion

    #region Members

    // Capture the current synchronization context (e.g., UI thread context)
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private Timer? _autoUpdateCheckTimer;
    private string _assetRegexPattern = EntryApplication.GenericRuntimeIdentifier;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the <see cref="SynchronizationContext"/> used to dispatch events.<br/>
    /// When set to a non-null value, events such as <see cref="CheckForUpdateCompleted"/>, <see cref="UpdateFound"/>,
    /// <see cref="DownloadCompleted"/>, and <see cref="InstallUpdateCompleted"/> will be raised on the specified context.<br/>
    /// This is useful for UI applications where events need to be handled on the UI thread.
    /// </summary>
    /// <remarks>
    /// By default, the context is captured from <see cref="SynchronizationContext.Current"/> at construction time.<br/>
    /// Set to <c>null</c> to disable context dispatching and raise events on the calling thread.<br/>
    /// For UI applications, typically set this to <c>SynchronizationContext.Current</c> from the UI thread,
    /// or for Avalonia use <c>AvaloniaSynchronizationContext.Current</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Capture UI context when creating the manager on the UI thread
    /// var manager = new UpdatumManager(...);
    /// // Context is captured automatically, or set explicitly:
    /// manager.EventSynchronizationContext = SynchronizationContext.Current;
    /// </code>
    /// </example>
    public SynchronizationContext? EventSynchronizationContext { get; set; } = SynchronizationContext.Current;

    /// <summary>
    /// Gets the GitHub client used to access releases and release asset metadata through the GitHub API.
    /// </summary>
    public GitHubClient GithubClient { get; } =
        new(new Octokit.ProductHeaderValue(EntryApplication.AssemblyName ?? nameof(UpdatumManager),
            EntryAssemblyVersion.ToString()));

    /// <summary>
    /// Gets the HTTP client used to download release assets and checksum files.
    /// </summary>
    /// <remarks>Defaults to <see cref="HttpClient"/>. Set this during construction to customize transport behavior.</remarks>
    public HttpClient AssetHttpClient { get; init; } = HttpClient;

    /// <summary>
    /// Gets or sets whether a GitHub SHA-256 digest or matching SHA-256 sidecar is required before a downloaded
    /// update is accepted.
    /// </summary>
    public bool RequireAssetChecksum
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the suffix appended to a release asset name to locate its fallback SHA-256 checksum asset.
    /// </summary>
    /// <remarks>Defaults to <c>.sha256</c>.</remarks>
    public string AssetChecksumSuffix
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            RaiseAndSetIfChanged(ref field, value);
        }
    } = ".sha256";

    /// <summary>
    /// Gets or sets an application-provided verifier for platform-specific asset signatures.
    /// </summary>
    /// <remarks>
    /// The callback receives the downloaded file path and should validate the signature and trust chain appropriate
    /// for the package type, such as Authenticode on Windows or code signing on macOS.
    /// </remarks>
    public Func<string, CancellationToken, ValueTask<bool>>? AssetSignatureVerifier
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets whether download must fail when no signature verifier is configured or signature validation fails.
    /// </summary>
    public bool RequireAssetSignatureVerification
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets the auto updater timer. Use this to start or stop the timer for your timed auto checks.
    /// </summary>
    public Timer AutoUpdateCheckTimer
    {
        get
        {
            if (_autoUpdateCheckTimer is null)
            {
                _autoUpdateCheckTimer = new Timer(TimeSpan.FromHours(12))
                {
                    AutoReset = true
                };

                _autoUpdateCheckTimer.Elapsed += AutoUpdateCheckTimerOnElapsed;
            }

            return _autoUpdateCheckTimer;
        }
    }

    /// <summary>
    /// Gets or sets the current version of the application.
    /// </summary>
    public Version CurrentVersion { get; init; } = EntryAssemblyVersion;

    /// <summary>
    /// Gets the owner of the repository.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// Gets the name of the repository.
    /// </summary>
    public required string Repository { get; init; }

    /// <summary>
    /// Gets the full GitHub repository URL.
    /// </summary>
    public string RepositoryUrl => $"{GitHubUrl}/{Owner}/{Repository}";

    /// <summary>
    /// Gets or sets whatever to fetch only the latest release or all releases.
    /// Note that fetching all releases can waste more tokens and memory.
    /// </summary>
    /// <remarks>By default, it will only fetch 100 releases and 1 page to spare tokens, can be configurable via <see cref="GitHubApiOptions"/></remarks>
    public bool FetchOnlyLatestRelease
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether pre-release versions are allowed.<br />
    /// Example: v1.0.0-alpha, v1.0.0-beta, v1.0.0-rc1
    /// </summary>
    public bool AllowPreReleases
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets the last time the repository was checked for updates.
    /// </summary>
    public DateTime LastCheckDateTime
    {
        get;
        private set => RaiseAndSetIfChanged(ref field, value);
    } = DateTime.MinValue;

    /// <summary>
    /// Gets the list of all releases (unfiltered) for the repository.
    /// </summary>
    /// <remarks>Returns empty when never checked for update.</remarks>
    public IReadOnlyList<Release> Releases
    {
        get;
        private set => RaiseAndSetIfChanged(ref field, value);
    } = [];

    /// <summary>
    /// Gets the releases ahead of the current version.
    /// </summary>
    /// <remarks>Returns empty when never checked for update.</remarks>
    public IReadOnlyList<Release> ReleasesAhead
    {
        get;
        private set
        {
            if (!RaiseAndSetIfChanged(ref field, value)) return;
            RaisePropertyChanged(nameof(ReleasesAheadCount));
            RaisePropertyChanged(nameof(LatestRelease));
            RaisePropertyChanged(nameof(LatestReleaseTagVersionStr));
            RaisePropertyChanged(nameof(IsUpdateAvailable));
        }
    } = [];

    /// <summary>
    /// Gets the number of releases ahead of the current version.
    /// </summary>
    public int ReleasesAheadCount => ReleasesAhead.Count;

    /// <summary>
    /// Gets if there are any updates available.
    /// </summary>
    [MemberNotNullWhen(true, nameof(LatestRelease), nameof(LatestReleaseTagVersionStr))]
    public bool IsUpdateAvailable => ReleasesAheadCount > 0;

    /// <summary>
    /// Gets the latest release for the repository.
    /// </summary>
    public Release? LatestRelease => ReleasesAhead.Count > 0 ? ReleasesAhead[0] : null;

    /// <summary>
    /// Gets the latest release tag version.
    /// </summary>
    public string? LatestReleaseTagVersionStr => LatestRelease?.GetTagVersionStr();

    /// <summary>
    /// Gets the <see cref="Regex"/> object used to match the asset name.
    /// </summary>
    public Regex? AssetRegex { get; private set; }

    /// <summary>
    /// Gets or sets the regex pattern to match with the asset name.
    /// </summary>
    /// <remarks>Defaults to <see cref="EntryApplication.GenericRuntimeIdentifier"/>.</remarks>
    public string AssetRegexPattern
    {
        get => _assetRegexPattern;
        set
        {
            if (!RaiseAndSetIfChanged(ref _assetRegexPattern, value)) return;
            AssetRegex = string.IsNullOrWhiteSpace(value)
                ? null
                : new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// Gets or sets the required asset extension when multiple assets target the same platform.
    /// </summary>
    /// <remarks>Use this option when you have multiple assets for same platform, ie: Windows in MSI and ZIP.</remarks>
    public string? AssetExtensionFilter
    {
        get;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = value.Trim();
                if (value[0] != '.') value = $".{value}";
            }

            RaiseAndSetIfChanged(ref field, value);
        }
    }


    /// <summary>
    /// Gets the number of times the updater checked for updates.
    /// </summary>
    public int CheckForUpdateCount
    {
        get;
        private set => RaiseAndSetIfChanged(ref field, value);
    }


    /// <summary>
    /// Gets or sets the interval in seconds to update the download progress statistics.
    /// </summary>
    /// <remarks>A value of 0 will always report the progress on each read chunk.</remarks>
    public double DownloadProgressUpdateFrequencySeconds
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    } = 0.1;

    /// <summary>
    /// Gets the total size of the download in bytes.
    /// </summary>
    public long DownloadSizeBytes
    {
        get;
        private set
        {
            if (!RaiseAndSetIfChanged(ref field, value)) return;
            RaisePropertyChanged(nameof(DownloadSizeMegabytes));
            RaisePropertyChanged(nameof(DownloadedPercentage));
        }
    } = -1;

    /// <summary>
    /// Gets the current downloaded size in bytes.
    /// </summary>
    public long DownloadedBytes
    {
        get;
        private set
        {
            if (!RaiseAndSetIfChanged(ref field, value)) return;
            RaisePropertyChanged(nameof(DownloadedMegabytes));
            RaisePropertyChanged(nameof(DownloadedPercentage));
        }
    }

    /// <summary>
    /// Gets the total size of the download in megabytes.
    /// </summary>
    public double DownloadSizeMegabytes => DownloadSizeBytes > 0
        ? Math.Round(DownloadSizeBytes / 1024.0 / 1024.0, 2, MidpointRounding.AwayFromZero)
        : double.NaN;

    /// <summary>
    /// Gets the current downloaded size in megabytes.
    /// </summary>
    public double DownloadedMegabytes => DownloadedBytes > 0
        ? Math.Round(DownloadedBytes / 1024.0 / 1024.0, 2, MidpointRounding.AwayFromZero)
        : 0.0;

    /// <summary>
    /// Gets the current downloaded percentage of the progress from 0% to 100%.
    /// </summary>
    public double DownloadedPercentage => DownloadSizeBytes > 0
        ? Math.Round(DownloadedBytes / (double)DownloadSizeBytes * 100.0, 2, MidpointRounding.AwayFromZero)
        : 0.0;

    /// <summary>
    /// Gets or sets the type of Windows executable used to install updates.<br/>
    /// Use <see cref="UpdatumWindowsExeType.Auto"/> to let the updater infer the type based on the asset file signature.<br/>
    /// Use <see cref="UpdatumWindowsExeType.Installer"/> for installer packages (.exe installers).<br/>
    /// Use <see cref="UpdatumWindowsExeType.SingleFileApp"/> for single-file executables (.exe).<br/>
    /// </summary>
    /// <remarks>Executable files (.exe) can either be installers or single-file apps, the recommendations are:<br/>
    /// - If you have the two types on your assets leave this to <see cref="UpdatumWindowsExeType.Auto"/>,
    /// this can lead to false positives if your app have raw strings that share installer signatures, eg. 'Inno Setup'.<br/>
    /// - If you build and only have single-file app or installer, configure accordingly to prevent false positives.</remarks>
    public UpdatumWindowsExeType InstallUpdateWindowsExeType
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the arguments to pass to the installer when using the auto installer under Windows.
    /// </summary>
    /// <remarks>For msi, exe. Can be used for a silent installation.</remarks>
    /// <example>/qb = Basic MSI installation.</example>
    public string? InstallUpdateWindowsInstallerArguments
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the strategy used to determine the executable file name when installing an update for single-file applications.
    /// </summary>
    public UpdatumSingleFileExecutableNameStrategy InstallUpdateSingleFileExecutableNameStrategy
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// <p>Gets or sets the fallback name of the single file executable or directory to use for the auto updater when unable to infer from current running file.</p>
    /// <p>Use {0} token to be replaced with the downloaded tag version.</p>
    /// <p>A null or empty value will use the downloaded file name instead as fallback.</p>
    /// </summary>
    /// <example>MyAppName_v{0}</example>
    public string? InstallUpdateSingleFileExecutableName
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the auto updater should locally code-sign macOS applications.
    /// </summary>
    public bool InstallUpdateCodesignMacOSApp
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets a custom script to inject into the auto updater script.
    /// Will be injected before run the upgraded application.
    /// </summary>
    public string? InstallUpdateInjectCustomScript
    {
        get;
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Gets the current state of the updater.
    /// </summary>
    public UpdatumState State
    {
        get;
        private set
        {
            if (!RaiseAndSetIfChanged(ref field, value)) return;
            RaisePropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>
    /// Gets if the updater is busy doing any check or operation.
    /// </summary>
    public bool IsBusy => State != UpdatumState.None;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatumManager"/> class and try to infer the <see cref="Owner"/> and <see cref="Repository"/> from RepositoryUrl from the assembly metadata.
    /// </summary>
    /// <remarks>Warning: Only use this constructor when the RepositoryUrl is well-defined on your entry assembly, or it will throw exceptions.</remarks>
    /// <exception cref="InvalidOperationException">When unable to infer from the RepositoryUrl.</exception>
    public UpdatumManager()
    {
        if (!string.IsNullOrWhiteSpace(_assetRegexPattern))
        {
            AssetRegex = new Regex(_assetRegexPattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatumManager"/> class with the specified parameters.
    /// </summary>
    /// <param name="repositoryUrl">The full GitHub repository url, must starts with: https://github.com/, if null or empty it will try to infer from the assembly RepositoryUrl metadata.</param>
    /// <param name="currentVersion">Your app version that is current running, if <c>null</c>, it will fetch the version from EntryAssembly.</param>
    /// <param name="gitHubCredentials">Pass the GitHub credentials if required, for extra tokens or visibility.</param>
    /// <exception cref="ArgumentException">When unable to infer from the <paramref name="repositoryUrl"/>.</exception>
    [SetsRequiredMembers]
    public UpdatumManager(string? repositoryUrl, Version? currentVersion = null,
        Credentials? gitHubCredentials = null) : this()
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl)) repositoryUrl = EntryApplication.AssemblyRepositoryUrl;

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            throw new ArgumentNullException(nameof(repositoryUrl),
                "Unable to infer from the RepositoryUrl, maybe missing from assembly metadata.");
        }

        if (!repositoryUrl.StartsWith(GitHubUrl, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unable to infer from the url, expecting to start with: <{GitHubUrl}>, got <{repositoryUrl}>.",
                nameof(repositoryUrl));
        }

        var match = Regex.Match(repositoryUrl, @$"{Regex.Escape(GitHubUrl)}\/([a-zA-Z0-9-]+)\/([a-zA-Z\d\-_.]+)",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new ArgumentException("Unable to infer from the url, regex failed to acquire owner/repo.",
                nameof(repositoryUrl));
        }

        if (match.Groups.Count < 3)
        {
            throw new ArgumentException(
                $"Unable to infer from the url, regex failed to acquire the groups, expecting >=3, got {match.Groups.Count}.",
                nameof(repositoryUrl));
        }

        if (currentVersion is not null) CurrentVersion = currentVersion;
        if (gitHubCredentials is not null) GithubClient.Credentials = gitHubCredentials;
        Owner = match.Groups[1].Value;
        Repository = match.Groups[2].Value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatumManager"/> class with the specified parameters.
    /// </summary>
    /// <param name="owner">The repository owner</param>
    /// <param name="repository">The repository name</param>
    /// <param name="currentVersion">Your app version that is current running, if <c>null</c>, it will fetch the version from EntryAssembly.</param>
    /// <param name="gitHubCredentials">Pass the GitHub credentials if required, for extra tokens or visibility.</param>
    [SetsRequiredMembers]
    public UpdatumManager(string owner, string repository, Version? currentVersion = null,
        Credentials? gitHubCredentials = null) : this()
    {
        if (currentVersion is not null) CurrentVersion = currentVersion;
        if (gitHubCredentials is not null) GithubClient.Credentials = gitHubCredentials;
        Owner = owner;
        Repository = repository;
    }

    [SetsRequiredMembers]
    internal UpdatumManager(string owner, string repository, GitHubClient githubClient) : this(owner, repository)
    {
        ArgumentNullException.ThrowIfNull(githubClient);
        GithubClient = githubClient;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Checks for updates in the repository.
    /// </summary>
    /// <param name="baseVersion">The version against which available releases are compared.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><c>True</c> if an update is found; otherwise, <c>false</c>.</returns>
    public async Task<bool> CheckForUpdatesAsync(Version baseVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseVersion);
        ThrowIfDisposed();
        using var linkedCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationSource.Token);
        var operationCancellationToken = linkedCancellationSource.Token;
        if (!await _operationGate.WaitAsync(0, operationCancellationToken).ConfigureAwait(false)) return false;

        var timerState = _autoUpdateCheckTimer is not null && _autoUpdateCheckTimer.Enabled;

        try
        {
            State = UpdatumState.CheckingForUpdate;
            LastCheckDateTime = DateTime.Now;
            CheckForUpdateCount++;

            // Stop the timer while checking to prevent overlapping elapsed callbacks.
            if (timerState) AutoUpdateCheckTimer.Stop();

            if (FetchOnlyLatestRelease)
            {
                var release = await GithubClient.Repository.Release.GetLatest(Owner, Repository)
                    .WaitAsync(operationCancellationToken).ConfigureAwait(false);
                Releases = [release];
            }
            else
            {
                Releases = await GithubClient.Repository.Release.GetAll(Owner, Repository, GitHubApiOptions)
                    .WaitAsync(operationCancellationToken).ConfigureAwait(false);
            }

            var releasesAheadList = new List<Release>(Releases.Count);

            foreach (var release in Releases)
            {
                if (release.Draft // Skip draft releases
                    || release.PublishedAt is null // Skip not published releases
                    || release.Assets.Count == 0 // Skip releases without assets
                    || (!AllowPreReleases && release.Prerelease)) // Skip pre-releases if not allowed
                    continue;

                var tagVersion = release.GetTagVersion();

                if (tagVersion is null) continue;
                if (tagVersion.CompareTo(baseVersion) <= 0)
                    break; // If the release version is less than or equal to the current version, break it.
                if (GetCompatibleReleaseAsset(release) is null) continue; // Skip releases without matching assets

                releasesAheadList.Add(release);
            }

            ReleasesAhead = releasesAheadList;
            if (IsUpdateAvailable) RaiseEvent(UpdateFound);
        }
        finally
        {
            State = UpdatumState.None;
            _operationGate.Release();
            if (timerState && !IsDisposed)
            {
                AutoUpdateCheckTimer.Start();
            }

            RaiseEvent(CheckForUpdateCompleted);
        }

        return IsUpdateAvailable;
    }

    /// <summary>
    /// Checks for updates in the repository relative to <see cref="CurrentVersion"/>.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><c>True</c> if an update is found; otherwise, <c>false</c>.</returns>
    public Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        return CheckForUpdatesAsync(CurrentVersion, cancellationToken);
    }

    /// <summary>
    /// Gets the formatted changelog of the releases ahead of the current version.
    /// </summary>
    /// <param name="maxReleases">The maximum number of releases to return. Use a value &lt;= 0 to return all updates.</param>
    /// <param name="reverseDisplayOrder"><see langword="true"/> reverses the output order so the latest version appears at the end instead of the beginning.</param>
    /// <returns>The formatted changelog, or <see langword="null"/> when no update is available.</returns>
    public string? GetChangelog(int maxReleases = -1, bool reverseDisplayOrder = false)
    {
        if (!IsUpdateAvailable) return null;

        var sb = new StringBuilder();

        var count = 0;
        var releaseDiffNumber = ReleasesAhead.Count;
        var list = reverseDisplayOrder ? ReleasesAhead.Reverse() : ReleasesAhead;

        foreach (var release in list)
        {
            count++;
            if (maxReleases > 0 && count > maxReleases) break;

            sb.AppendLine($"## {release.Name}");
            sb.AppendLine();
            sb.AppendLine($"> Release date: {release.PublishedAt}  ");
            sb.AppendLine($"> Release diff: {(reverseDisplayOrder ? count : releaseDiffNumber)}");
            sb.AppendLine();
            sb.AppendLine(release.Body);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            releaseDiffNumber--;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the formatted changelog of the releases ahead of the current version.
    /// </summary>
    /// <param name="reverseDisplayOrder"><see langword="true"/> reverses the output order so the latest version appears at the end instead of the beginning.</param>
    /// <param name="maxReleases">The maximum number of releases to return. Use a value &lt;= 0 to return all updates.</param>
    /// <returns>The formatted changelog, or <see langword="null"/> when no update is available.</returns>
    public string? GetChangelog(bool reverseDisplayOrder, int maxReleases = -1)
    {
        return GetChangelog(maxReleases, reverseDisplayOrder);
    }

    /// <summary>
    /// Gets the correct and compatible <see cref="ReleaseAsset"/> for the running system and application type
    /// based on the <see cref="AssetRegexPattern"/> and <see cref="AssetExtensionFilter"/>.<br/>
    /// </summary>
    /// <param name="release">The release where you want to get the compatible asset.</param>
    /// <remarks>
    /// When multiple matching assets are found without providing <see cref="AssetExtensionFilter"/>,
    /// it will try to infer based on `EntryApplication` bundle type, which searches and defaults to:<br/>
    ///   - Windows:<br/>
    ///     - <c>.exe</c> if running under single-file (`PublishSingleFile`)<br/>
    ///     - Otherwise, defaults to <c>.msi</c><br/>
    ///   - Linux:<br/>
    ///     - <c>AppImage</c>, <c>Flatpak</c>, <c>.deb</c>, <c>.rpm</c>, <c>.pkg.tar.zst</c>, or <c>.snap</c>
    ///       when running from the corresponding package type<br/>
    ///     - Otherwise, defaults to <c>.zip</c><br/>
    ///   - If none of the above matches, it will fall back to the first matching asset
    /// </remarks>
    /// <returns>The <see cref="ReleaseAsset"/> for the current system and app type, if not found, return <c>null</c>.</returns>
    public ReleaseAsset? GetCompatibleReleaseAsset(Release release)
    {
        if (release.Assets.Count == 0) return null;

        var checksumAssetNames = release.Assets
            .Select(asset => $"{asset.Name}{AssetChecksumSuffix}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateAssets = new List<ReleaseAsset>();
        foreach (var asset in release.Assets)
        {
            if (checksumAssetNames.Contains(asset.Name)) continue;
            if (AssetRegex is not null && !AssetRegex.IsMatch(asset.Name)) continue;
            if (!string.IsNullOrWhiteSpace(AssetExtensionFilter) &&
                !asset.Name.EndsWith(AssetExtensionFilter, StringComparison.OrdinalIgnoreCase)) continue;
            candidateAssets.Add(asset);
        }

        if (candidateAssets.Count == 0) return null;
        if (candidateAssets.Count == 1) return candidateAssets[0];

        // Multiple assets found, no extension filter is set, perform extra guess check.
        // Try to infer the best asset based on the EntryApplication bundle.
        if (string.IsNullOrWhiteSpace(AssetExtensionFilter))
        {
            foreach (var extension in GetPreferredAssetExtensions())
            {
                var preferredAsset =
                    candidateAssets.FirstOrDefault(asset => AssetNameMatchesExtension(asset.Name, extension));
                if (preferredAsset is not null) return preferredAsset;
            }
        }

        return candidateAssets[0];
    }

    /// <summary>
    /// Gets the asset extensions to try, most preferred first, for the current packaging type.
    /// </summary>
    /// <returns>An ordered array of extensions.</returns>
    /// <remarks>
    /// macOS reports <see cref="ApplicationPackagingType.MacOSAppBundle"/> regardless of how the bundle was
    /// originally delivered, so the bundle formats fall back to each other instead of resolving to a single
    /// extension.
    /// </remarks>
    internal static string[] GetPreferredAssetExtensions()
    {
        if (EntryApplication.PackagingType is ApplicationPackagingType.DotNetSingleFile)
        {
            if (OperatingSystem.IsWindows())
            {
                return [".exe", ".msi", ".zip"];
            }

            return
            [
                ApplicationPackagingInfo.KnownPackagingTypes[ApplicationPackagingType.DotNetSingleFile].Extensions
                    .FirstOrDefault(ext =>
                        !string.IsNullOrWhiteSpace(ext) &&
                        !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)) ??
                ".bin"
            ];
        }

        return ApplicationPackagingInfo.KnownPackagingTypes
            .Where(kvp => kvp.Value.IsSupportedOnCurrentPlatform
                          && kvp.Key is not (ApplicationPackagingType.None or ApplicationPackagingType.DotNetSingleFile)
                          && (kvp.Key == EntryApplication.PackagingType || !kvp.Value.DistroSpecific))
            .OrderBy(pair => pair.Key == EntryApplication.PackagingType ? 0
                : pair.Key == ApplicationPackagingType.Portable ? 2
                : 1)
            .SelectMany(pair => pair.Value.Extensions)
            .DistinctBy(s => s)
            .ToArray();
    }


    private static bool AssetNameMatchesExtension(string assetName, string extension)
    {
        return string.IsNullOrWhiteSpace(extension)
            ? string.IsNullOrWhiteSpace(Path.GetExtension(assetName))
            : assetName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Downloads the latest release for the current system.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="UpdatumDownloadedAsset"/> object, otherwise returns null if failed.</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="OperationCanceledException"/>
    /// <exception cref="HttpRequestException"/>
    public Task<UpdatumDownloadedAsset?> DownloadUpdateAsync(CancellationToken cancellationToken)
    {
        return DownloadUpdateAsync(null, cancellationToken);
    }

    /// <summary>
    /// Downloads a release for the current system.
    /// </summary>
    /// <param name="release">The release to download.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="UpdatumDownloadedAsset"/> object, otherwise returns null if failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when release is null after resolution.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <exception cref="ApiException">Thrown when GitHub release asset metadata cannot be retrieved.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="IOException">Thrown when file operations fail.</exception>
    public async Task<UpdatumDownloadedAsset?> DownloadUpdateAsync(Release? release = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var linkedCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationSource.Token);
        var operationCancellationToken = linkedCancellationSource.Token;
        if (!await _operationGate.WaitAsync(0, operationCancellationToken).ConfigureAwait(false)) return null;

        string? temporaryDirectoryPath = null;
        UpdatumDownloadedAsset? download = null;

        try
        {
            State = UpdatumState.DownloadingUpdate;
            DownloadedBytes = 0;
            DownloadSizeBytes = -1;

            release ??= LatestRelease;
            ArgumentNullException.ThrowIfNull(release);

            var asset = GetCompatibleReleaseAsset(release);
            if (asset is null) return null;

            ValidateAssetFileName(asset.Name);
            temporaryDirectoryPath = CreateDownloadWorkspace(
                EntryApplication.IsLinuxFlatpak,
                Environment.GetEnvironmentVariable("XDG_CACHE_HOME"));
            var targetPath = Path.Combine(temporaryDirectoryPath, asset.Name);

            using var request = CreateAssetRequest(asset);

            using var response = await AssetHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationCancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
            DownloadSizeBytes = totalBytes > 0 ? totalBytes : -1;

            long totalRead = 0;
            await using (var fileStream = new SafeFileStream(targetPath))
            await using (var contentStream =
                         await response.Content.ReadAsStreamAsync(operationCancellationToken).ConfigureAwait(false))
            {
                var lastReportTime = Stopwatch.GetTimestamp();
                var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                try
                {
                    int bytesRead;
                    while ((bytesRead = await contentStream
                               .ReadAsync(buffer.AsMemory(0, buffer.Length), operationCancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        await fileStream
                            .WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), operationCancellationToken)
                            .ConfigureAwait(false);
                        totalRead += bytesRead;

                        // Display progress every x seconds or on final chunk
                        var currentTimestamp = Stopwatch.GetTimestamp();
                        var elapsedTimeSpan = Stopwatch.GetElapsedTime(lastReportTime, currentTimestamp);
                        if (elapsedTimeSpan.TotalSeconds >= DownloadProgressUpdateFrequencySeconds ||
                            totalRead == totalBytes)
                        {
                            DownloadedBytes = totalRead;
                            lastReportTime = currentTimestamp;
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                await fileStream.CommitAsync(operationCancellationToken).ConfigureAwait(false);
            }

            if (totalBytes > 0 && totalRead != totalBytes)
            {
                throw new InvalidDataException(
                    $"Downloaded asset length was {totalRead} bytes; expected {totalBytes} bytes.");
            }

            DownloadedBytes = totalRead;
            var sha256 = await VerifyAssetChecksumAsync(release, asset, targetPath, operationCancellationToken)
                .ConfigureAwait(false);
            var isSignatureVerified = await VerifyAssetSignatureAsync(targetPath, operationCancellationToken)
                .ConfigureAwait(false);
            download = new UpdatumDownloadedAsset(release, asset, targetPath)
            {
                Sha256 = sha256,
                IsSignatureVerified = isSignatureVerified,
                TemporaryDirectoryPath = temporaryDirectoryPath
            };
        }
        catch
        {
            DownloadedBytes = 0;
            DeleteTemporaryWorkspace(temporaryDirectoryPath);

            throw;
        }
        finally
        {
            State = UpdatumState.None;
            _operationGate.Release();
        }

        if (download is not null) RaiseEvent(DownloadCompleted, download);
        return download;
    }

    private HttpRequestMessage CreateAssetRequest(ReleaseAsset asset)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, asset.BrowserDownloadUrl);

        // Browser download URLs cannot authenticate downloads from private repositories.
        if (GithubClient.Credentials.AuthenticationType == AuthenticationType.Anonymous) return request;

        request.RequestUri = GetTrustedGitHubApiUri(asset.Url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        switch (GithubClient.Credentials.AuthenticationType)
        {
            case AuthenticationType.Basic:
                var header =
                    Convert.ToBase64String(
                        Encoding.ASCII.GetBytes(
                            $"{GithubClient.Credentials.Login}:{GithubClient.Credentials.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", header);
                break;
            case AuthenticationType.Oauth:
            case AuthenticationType.Bearer:
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", GithubClient.Credentials.GetToken());
                break;
            case AuthenticationType.Anonymous:
                break;
            default:
                request.Dispose();
                throw new InvalidOperationException(
                    $"Unexpected value for {nameof(GithubClient.Credentials.AuthenticationType)}: {GithubClient.Credentials.AuthenticationType}");
        }

        return request;
    }

    private async Task<string?> VerifyAssetChecksumAsync(
        Release release,
        ReleaseAsset asset,
        string targetPath,
        CancellationToken cancellationToken)
    {
        var gitHubDigest = await GetGitHubAssetDigestAsync(asset, cancellationToken).ConfigureAwait(false);
        Debug.WriteLine($"GitHub digest for release asset '{asset.Name}': {gitHubDigest}");
        if (!string.IsNullOrWhiteSpace(gitHubDigest) &&
            gitHubDigest.StartsWith(GitHubSha256DigestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var hashText = gitHubDigest[GitHubSha256DigestPrefix.Length..];
            if (hashText.Length != 64 || !Sha256Regex().IsMatch(hashText))
            {
                throw new InvalidDataException(
                    $"GitHub returned an invalid SHA-256 digest for release asset '{asset.Name}'.");
            }

            Debug.WriteLine($"Verifying SHA-256 digest for release asset '{asset.Name}': {hashText}");

            return await VerifySha256Async(asset, targetPath, Convert.FromHexString(hashText), cancellationToken)
                .ConfigureAwait(false);
        }

        var checksumAssetName = $"{asset.Name}{AssetChecksumSuffix}";
        var checksumAsset = release.Assets.FirstOrDefault(candidate =>
            candidate.Name.Equals(checksumAssetName, StringComparison.OrdinalIgnoreCase));

        if (checksumAsset is null)
        {
            if (RequireAssetChecksum)
            {
                throw new InvalidDataException(
                    $"Release asset '{asset.Name}' has neither a GitHub SHA-256 digest nor a matching " +
                    $"'{checksumAssetName}' checksum asset.");
            }

            return null;
        }

        using var request = CreateAssetRequest(checksumAsset);
        using var response = await AssetHttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var checksumText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (checksumText.Length > 4096)
        {
            throw new InvalidDataException($"Checksum asset '{checksumAsset.Name}' is unexpectedly large.");
        }

        var match = Sha256Regex().Match(checksumText);
        if (!match.Success)
        {
            throw new InvalidDataException($"Checksum asset '{checksumAsset.Name}' does not contain a SHA-256 hash.");
        }

        return await VerifySha256Async(asset, targetPath, Convert.FromHexString(match.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    internal virtual async Task<string?> GetGitHubAssetDigestAsync(
        ReleaseAsset asset,
        CancellationToken cancellationToken)
    {
        var assetApiUri = GetTrustedGitHubApiUri(asset.Url);
        var response = await GithubClient.Connection
            .Get<GitHubReleaseAssetMetadata>(
                assetApiUri,
                new Dictionary<string, string>(),
                GitHubApiMediaType,
                cancellationToken)
            .ConfigureAwait(false);

        return response.Body.Digest;
    }

    private Uri GetTrustedGitHubApiUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidDataException($"Release asset API URL '{url}' is not an absolute URL.");
        }

        var baseAddress = GithubClient.Connection.BaseAddress;
        if (!uri.Scheme.Equals(baseAddress.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !uri.IdnHost.Equals(baseAddress.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != baseAddress.Port)
        {
            throw new InvalidDataException(
                $"Release asset API URL '{uri}' does not match the configured GitHub API origin '{baseAddress}'.");
        }

        return uri;
    }

    private static async Task<string> VerifySha256Async(
        ReleaseAsset asset,
        string targetPath,
        byte[] expectedHash,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(targetPath, System.IO.FileMode.Open, FileAccess.Read,
            FileShare.Read, DefaultBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(fileStream, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
        {
            throw new InvalidDataException($"SHA-256 verification failed for release asset '{asset.Name}'.");
        }

        Debug.WriteLine(
            $"SHA-256 verification succeeded for release asset '{asset.Name}': {Convert.ToHexString(actualHash)}");

        return Convert.ToHexString(actualHash);
    }

    private sealed record GitHubReleaseAssetMetadata
    {
        public string? Digest { get; init; }
    }

    private async ValueTask<bool> VerifyAssetSignatureAsync(string targetPath, CancellationToken cancellationToken)
    {
        if (AssetSignatureVerifier is null)
        {
            if (RequireAssetSignatureVerification)
            {
                throw new InvalidDataException(
                    "Asset signature verification is required, but no verifier is configured.");
            }

            return false;
        }

        if (!await AssetSignatureVerifier(targetPath, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidDataException(
                $"Signature verification failed for release asset '{Path.GetFileName(targetPath)}'.");
        }

        return true;
    }

    private static void ValidateAssetFileName(string assetName)
    {
        if (!FileUtilities.IsPathLeafName(assetName))
        {
            throw new InvalidDataException($"Release asset name '{assetName}' is not a safe file name.");
        }
    }

    private static void DeleteTemporaryWorkspace(string? temporaryDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(temporaryDirectoryPath)) return;

        try
        {
            if (Directory.Exists(temporaryDirectoryPath)) Directory.Delete(temporaryDirectoryPath, true);
        }
        catch
        {
            // Cleanup is best effort and must not hide the original failure.
        }
    }

    internal static string CreateDownloadWorkspace(bool isLinuxFlatpak, string? flatpakCacheDirectory)
    {
        string parentDirectoryPath;
        if (isLinuxFlatpak)
        {
            if (string.IsNullOrWhiteSpace(flatpakCacheDirectory))
            {
                throw new InvalidOperationException(
                    "A Flatpak update requires the XDG_CACHE_HOME environment variable for host-visible staging.");
            }

            parentDirectoryPath = Path.GetFullPath(flatpakCacheDirectory);
            Directory.CreateDirectory(parentDirectoryPath);
        }
        else
        {
            parentDirectoryPath = Path.GetTempPath();
        }

        return Directory.CreateDirectory(
                Path.Combine(parentDirectoryPath, $"StageKit.Updatum-{Guid.NewGuid():N}"))
            .FullName;
    }

    internal static LinuxPackageInstallCommand CreateLinuxPackageInstallCommand(
        string filePath,
        LinuxPackageManager packageManager,
        FlatpakInstallationScope flatpakInstallationScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var packagingType = GetPackagingTypeForFile(filePath, OSPlatform.Linux);
        if (packagingType is ApplicationPackagingType.LinuxFlatpak)
        {
            var isSystemInstallation = flatpakInstallationScope is FlatpakInstallationScope.System;
            return new LinuxPackageInstallCommand(
                "Flatpak",
                "flatpak",
                [
                    isSystemInstallation ? "--system" : "--user",
                    "install",
                    "--or-update",
                    "--noninteractive",
                    filePath
                ],
                isSystemInstallation);
        }

        if (packagingType is ApplicationPackagingType.LinuxDeb)
        {
            return packageManager is LinuxPackageManager.Apt
                ? new LinuxPackageInstallCommand("Debian", "apt-get", ["install", "--yes", filePath], true)
                : new LinuxPackageInstallCommand("Debian", "dpkg", ["--install", filePath], true);
        }

        if (packagingType is ApplicationPackagingType.LinuxRpm)
        {
            return packageManager switch
            {
                LinuxPackageManager.Dnf5 =>
                    new LinuxPackageInstallCommand("RPM", "dnf5", ["install", "--assumeyes", filePath], true),
                LinuxPackageManager.Dnf =>
                    new LinuxPackageInstallCommand("RPM", "dnf", ["install", "--assumeyes", filePath], true),
                LinuxPackageManager.Yum =>
                    new LinuxPackageInstallCommand("RPM", "yum", ["install", "--assumeyes", filePath], true),
                LinuxPackageManager.Zypper =>
                    new LinuxPackageInstallCommand("RPM", "zypper", ["--non-interactive", "install", filePath],
                        true),
                _ => new LinuxPackageInstallCommand("RPM", "rpm", ["--upgrade", "--replacepkgs", filePath], true)
            };
        }

        if (packagingType is ApplicationPackagingType.LinuxArchPackage)
        {
            return new LinuxPackageInstallCommand(
                "Arch Linux",
                "pacman",
                ["--upgrade", "--noconfirm", filePath],
                true);
        }

        if (packagingType is ApplicationPackagingType.LinuxSnap)
        {
            return new LinuxPackageInstallCommand("Snap", "snap", ["install", "--dangerous", filePath], true);
        }

        throw new NotSupportedException($"The Linux package file '{Path.GetFileName(filePath)}' is not supported.");
    }

    internal static void EnsurePackageInstallationSucceeded(string packageType, int exitCode)
    {
        if (exitCode == 0) return;

        if (ProcessHelper.IsExitCodeElevationDenied(exitCode))
            throw new IOException(
                $"{packageType} installation requires administrator privileges that were not granted " +
                $"(error code: {exitCode}).");

        throw new IOException($"{packageType} installation failed with error code: {exitCode}.");
    }

    /// <summary>
    /// Runs a generated macOS installation script and waits for it to complete.
    /// </summary>
    /// <param name="scriptFilePath">The generated script to run.</param>
    /// <param name="workingDirectory">The working directory for the script process.</param>
    /// <param name="requireElevation"><c>True</c> to request administrator privileges.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The script exit code.</returns>
    /// <remarks>
    /// <paramref name="workingDirectory"/> only reaches the script when <paramref name="requireElevation"/> is
    /// <c>false</c> or the process is already privileged; the elevated path launches through <c>osascript</c>.
    /// The script therefore anchors itself with its own directory.
    /// </remarks>
    private static Task<int> RunMacOSInstallScriptAsync(
        string scriptFilePath,
        string workingDirectory,
        bool requireElevation,
        CancellationToken cancellationToken)
    {
        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(scriptFilePath, requireElevation);
        startInfo.WorkingDirectory = workingDirectory;

        return ProcessHelper.StartProcessAsync(
            startInfo,
            true,
            PackageInstallTimeoutMilliseconds,
            cancellationToken);
    }

    internal static ApplicationPackagingType? GetPackagingTypeForFile(
        string filePath,
        OSPlatform? targetPlatform = null)
    {
        return ApplicationPackagingInfo.KnownPackagingTypes.Values
            .OrderBy(info => info.SupportedPlatform is null ? 1 : 0)
            .FirstOrDefault(info =>
                info.Extensions.Length > 0 &&
                (info.SupportedPlatform is null ||
                 (targetPlatform.HasValue
                     ? info.SupportedPlatform.Value.Equals(targetPlatform.Value)
                     : info.IsSupportedOnCurrentPlatform)) &&
                info.Extensions.Any(extension => AssetNameMatchesExtension(filePath, extension)))
            ?.PackagingType;
    }

    private static string ResolvePackageInstallerExecutable(LinuxPackageInstallCommand command)
    {
        if (command.Executable == "flatpak" && EntryApplication.IsLinuxFlatpak)
        {
            if (HostSystem.TryFindExecutable("flatpak-spawn", out _)) return command.Executable;

            throw new FileNotFoundException(
                "Flatpak host installation requires the 'flatpak-spawn' executable.",
                "flatpak-spawn");
        }

        if (HostSystem.TryFindExecutable(command.Executable, out var executablePath)) return executablePath;

        if (command.Executable == "apt-get" && HostSystem.TryFindExecutable("apt", out executablePath))
            return executablePath;

        throw new FileNotFoundException(
            $"{command.PackageType} installation requires the '{command.Executable}' executable.",
            command.Executable);
    }

    private static async Task<FlatpakInstallationScope> GetFlatpakInstallationScopeAsync(
        string flatpakExecutable,
        CancellationToken cancellationToken)
    {
        if (!EntryApplication.IsLinuxFlatpak || string.IsNullOrWhiteSpace(EntryApplication.LinuxFlatpakId))
            return FlatpakInstallationScope.User;

        var applicationId = EntryApplication.LinuxFlatpakId;
        var userExitCode = await ProcessHelper.StartHostProcessAsync(
                flatpakExecutable,
                ["--user", "info", applicationId],
                waitForCompletion: true,
                waitTimeout: 30_000,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (userExitCode == 0) return FlatpakInstallationScope.User;

        var systemExitCode = await ProcessHelper.StartHostProcessAsync(
                flatpakExecutable,
                ["--system", "info", applicationId],
                waitForCompletion: true,
                waitTimeout: 30_000,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (systemExitCode == 0) return FlatpakInstallationScope.System;

        throw new IOException(
            $"The current Flatpak installation '{applicationId}' was not found in the user or system installation.");
    }

    private static void StartUpdatedLinuxPackage(string filePath, string? runArguments)
    {
        var packagingType = GetPackagingTypeForFile(filePath);
        if (packagingType is ApplicationPackagingType.LinuxFlatpak)
        {
            var applicationId = EntryApplication.LinuxFlatpakId ?? Path.GetFileNameWithoutExtension(filePath);
            if (EntryApplication.IsLinuxFlatpak)
            {
                ProcessHelper.StartHostProcess("flatpak", ["run", applicationId]);
            }
            else if (HostSystem.TryFindExecutable("flatpak", out var flatpakExecutable))
            {
                ProcessHelper.StartHostProcess(flatpakExecutable, ["run", applicationId]);
            }

            return;
        }

        if (packagingType is ApplicationPackagingType.LinuxSnap)
        {
            var snapName = EntryApplication.LinuxSnapId ?? Path.GetFileNameWithoutExtension(filePath);
            if (HostSystem.TryFindExecutable("snap", out var snapExecutable))
                ProcessHelper.StartProcess(snapExecutable, ["run", snapName]);
            return;
        }

        if (!string.IsNullOrWhiteSpace(EntryApplication.ExecutablePath))
            ProcessHelper.StartProcess(EntryApplication.ExecutablePath, runArguments);
    }

    /// <summary>
    /// Downloads and installs the latest release and install for the current system.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="UpdatumDownloadedAsset"/> object, otherwise returns null if failed.</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="OperationCanceledException"/>
    /// <exception cref="HttpRequestException"/>
    /// <remarks>Note this function will never return True as program is terminated to upgrade.</remarks>
    public Task<bool> DownloadAndInstallUpdateAsync(CancellationToken cancellationToken)
    {
        return DownloadAndInstallUpdateAsync(null, cancellationToken);
    }

    /// <summary>
    /// Downloads and installs a release for the current system.
    /// </summary>
    /// <param name="release">The release to download.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="UpdatumDownloadedAsset"/> object, otherwise returns null if failed.</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="OperationCanceledException"/>
    /// <exception cref="HttpRequestException"/>
    /// <remarks>Note this function will never return True as program is terminated to upgrade.</remarks>
    public async Task<bool> DownloadAndInstallUpdateAsync(Release? release = null,
        CancellationToken cancellationToken = default)
    {
        var download = await DownloadUpdateAsync(release, cancellationToken).ConfigureAwait(false);
        if (download is null) return false;

        var installStarted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            installStarted = await InstallUpdateAsync(download, true, null, cancellationToken).ConfigureAwait(false);
            return installStarted;
        }
        finally
        {
            if (!installStarted) download.SafeDeleteFile();
        }
    }

    /// <summary>
    /// Installs the specified downloaded update asset asynchronously, replacing the current application or its components
    /// as needed.
    /// </summary>
    /// <remarks>This method supports a variety of update asset types, including portable executables, archives,
    /// Windows installers, and Linux AppImage, Flatpak, Debian, RPM, Arch Linux, or Snap packages. The installation
    /// process may involve extracting files, running platform-specific scripts, or invoking system installers. On successful installation, the current
    /// application may be terminated and the updated version launched, depending on the parameters provided. The method is
    /// cross-platform and handles platform-specific behaviors internally. If the update cannot be installed due to an
    /// unrecognized file type, the method returns false without throwing an exception.</remarks>
    /// <param name="downloadedAsset">The downloaded update asset to install. Must reference a valid, existing file containing the update package or
    /// installer.</param>
    /// <param name="forceTerminate">true to forcefully terminate the current application after starting the update installation; otherwise, false. If
    /// set to true, the process will exit to allow the update to complete safely.</param>
    /// <param name="runArguments">Optional command-line arguments to pass when launching the updated application after installation. If null or
    /// omitted, the default launch behavior is used. If a special token is provided to suppress relaunch, the application
    /// will not be started after the update.</param>
    /// <returns>A task that represents the asynchronous installation operation. The task result is true if the update was
    /// successfully initiated; otherwise, false if the file type was not recognized or installation could not proceed.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file specified by downloadedAsset does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown if the update file type is not supported on the current operating system.</exception>
    /// <exception cref="IOException">Thrown if a native package installer fails, including denied or unsuccessful elevation.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an unexpected value is encountered for a configuration property.</exception>
    public Task<bool> InstallUpdateAsync(UpdatumDownloadedAsset downloadedAsset, bool forceTerminate = true,
        string? runArguments = null)
    {
        return InstallUpdateAsync(downloadedAsset, forceTerminate, runArguments, CancellationToken.None);
    }

    /// <summary>
    /// Installs a downloaded update asset asynchronously.
    /// </summary>
    /// <param name="downloadedAsset">The downloaded update asset to install.</param>
    /// <param name="forceTerminate">Whether to terminate the current application after starting a deferred installer.</param>
    /// <param name="runArguments">Arguments passed to the updated application, or <see cref="NoRunAfterUpgradeToken"/> to suppress relaunch.</param>
    /// <param name="cancellationToken">A token used to cancel preparation and any directly executed installer.</param>
    /// <returns><c>True</c> when installation was started successfully; otherwise, <c>false</c>.</returns>
    public async Task<bool> InstallUpdateAsync(
        UpdatumDownloadedAsset downloadedAsset,
        bool forceTerminate,
        string? runArguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(downloadedAsset);
        if (!downloadedAsset.FileExists) throw new FileNotFoundException("File not found", downloadedAsset.FilePath);
        ThrowIfDisposed();
        using var linkedCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationSource.Token);
        var operationCancellationToken = linkedCancellationSource.Token;
        if (!await _operationGate.WaitAsync(0, operationCancellationToken).ConfigureAwait(false)) return false;

        try
        {
            State = UpdatumState.InstallingUpdate;
            return await InstallUpdateCoreAsync(downloadedAsset, forceTerminate, runArguments,
                operationCancellationToken).ConfigureAwait(false);
        }
        finally
        {
            State = UpdatumState.None;
            _operationGate.Release();
        }
    }

    internal virtual Task<bool> InstallUpdateCoreAsync(
        UpdatumDownloadedAsset downloadedAsset,
        bool forceTerminate,
        string? runArguments,
        CancellationToken cancellationToken)
    {
        var filePath = downloadedAsset.FilePath;
        var fileName = Path.GetFileName(filePath);
        var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var fileExtension = Path.GetExtension(fileName);

        var tmpPath = Path.GetTempPath();
        var installWorkspacePath = Directory.CreateTempSubdirectory("StageKit.Updatum.Install-").FullName;
        var currentVersion = EntryApplication.AssemblyVersion ?? CurrentVersion;
        var newVersionStr = downloadedAsset.TagVersionStr;

        TemporaryFile CreateScriptFile(out string scriptFilePath, out StreamWriter stream)
        {
            var extension = OperatingSystem.IsWindows() ? ".bat" : ".sh";
            var temporaryFile = new TemporaryFile(tmpPath, extension);
            scriptFilePath = temporaryFile.FilePath;

            try
            {
                stream = new StreamWriter(temporaryFile.Create())
                {
                    NewLine = "\n" // Use Unix line endings
                };
                temporaryFile.Keep();
                return temporaryFile;
            }
            catch
            {
                temporaryFile.Dispose();
                throw;
            }
        }

        // *************
        // ** Windows **
        // *************
        void WriteWindowsScriptHeader(StreamWriter stream)
        {
            stream.WriteLine("@echo off");
            stream.WriteLine("setlocal enabledelayedexpansion");
            stream.WriteLine();
            stream.WriteLine(
                $"REM Autogenerated by {nameof(UpdatumManager)} v{LibraryVersion.ToString(3)} [{DateTime.Now}]");
            stream.WriteLine($"REM {EntryApplication.AssemblyName} upgrade script");
            stream.WriteLine(
                $"echo \"{EntryApplication.AssemblyName} v{EntryApplication.AssemblyVersionString} -> {newVersionStr} updater script\"");
            stream.WriteLine("set \"DIR=%~dp0\"");
            stream.WriteLine("cd /d \"%DIR%\"");
            stream.WriteLine();

            // Set EntryApplication variables
            stream.WriteLine($"REM {nameof(EntryApplication)} variables");
            var info = EntryApplication.GetApplicationInfoDict();
            foreach (var kp in info)
            {
                stream.WriteLine($"set \"{kp.Key}={kp.Value.EscapeWindowsBatchValue()}\"");
            }

            stream.WriteLine();

            // Set variables
            stream.WriteLine("REM Variables");
            stream.WriteLine($"set \"oldVersion={EntryApplication.AssemblyVersionString.EscapeWindowsBatchValue()}\"");
            stream.WriteLine($"set \"newVersion={newVersionStr.EscapeWindowsBatchValue()}\"");
            stream.WriteLine($"set \"DOWNLOAD_FILEPATH={downloadedAsset.FilePath.EscapeWindowsBatchValue()}\"");
            stream.WriteLine(
                $"set \"DOWNLOAD_WORKSPACE_PATH={downloadedAsset.TemporaryDirectoryPath.EscapeWindowsBatchValue()}\"");
            stream.WriteLine($"set \"INSTALL_WORKSPACE_PATH={installWorkspacePath.EscapeWindowsBatchValue()}\"");
            stream.WriteLine($"set \"FILEPATH={filePath.EscapeWindowsBatchValue()}\"");
            stream.WriteLine($"set \"RUN_AFTER_UPGRADE={runArguments != NoRunAfterUpgradeToken}\"");
            stream.WriteLine($"set \"RUN_ARGUMENTS={runArguments.EscapeWindowsBatchValue()}\"");
            stream.WriteLine();
        }

        void WriteWindowsScriptFileValidation(StreamWriter stream)
        {
            // Downloaded file path verification
            stream.WriteLine("if not exist \"%DOWNLOAD_FILEPATH%\" (");
            stream.WriteLine("  echo - Error: The expected downloaded file does not exist");
            stream.WriteLine("  exit /b 1");
            stream.WriteLine(')');
            stream.WriteLine("if not exist \"%FILEPATH%\" (");
            stream.WriteLine("  echo - Error: The expected filepath does not exist");
            stream.WriteLine("  exit /b 1");
            stream.WriteLine(')');
            stream.WriteLine();
        }

        void WriteWindowsScriptKillInstances(StreamWriter stream)
        {
            var killCommands = new List<string>(2);

            if (EntryApplication.IsRunningFromDotNetProcess)
            {
                // Dangerous if script run again latter.
                //stream.WriteLine($"taskkill /pid {Environment.ProcessId} /f /t >nul 2>&1");
            }
            else if (!string.IsNullOrWhiteSpace(EntryApplication.ProcessName))
            {
                killCommands.Add($"taskkill /IM \"%{nameof(EntryApplication.ProcessName)}%\" /T >nul 2>&1");
            }

            if (!string.IsNullOrWhiteSpace(EntryApplication.AssemblyName))
            {
                var name = $"{EntryApplication.AssemblyName}.exe";
                if (killCommands.Count == 0 || EntryApplication.ProcessName != name)
                {
                    killCommands.Add($"taskkill /IM \"{name}\" /T >nul 2>&1");
                }
            }

            if (killCommands.Count > 0)
            {
                // Kill processes
                stream.WriteLine("echo \"- Killing processes\"");
                stream.WriteLine("timeout /t 1 /NOBREAK");
                stream.WriteLine();

                foreach (var killCommand in killCommands)
                {
                    stream.WriteLine(killCommand);
                }

                stream.WriteLine("timeout /t 2 /NOBREAK");

                foreach (var killCommand in killCommands)
                {
                    stream.WriteLine($"{killCommand} /F");
                }

                stream.WriteLine(" REM IM - Image name (process filename)");
                stream.WriteLine(" REM /F - Forceful termination (without this, it tries graceful first)");
                stream.WriteLine(" REM /T - Terminate child processes");
                stream.WriteLine("timeout /t 1 /NOBREAK");
                stream.WriteLine();
            }
        }

        void WriteWindowsScriptInjectCustomScript(StreamWriter stream)
        {
            if (!string.IsNullOrWhiteSpace(InstallUpdateInjectCustomScript))
            {
                stream.WriteLine("REM Custom script provided by the author.");
                stream.WriteLine(InstallUpdateInjectCustomScript);
                stream.WriteLine("REM End of custom script provided by the author.");
                stream.WriteLine();
            }
        }

        void WriteWindowsScriptEnd(StreamWriter stream)
        {
            stream.WriteLine("echo - Removing temp source files");
            stream.WriteLine("call :DeleteIfSafe \"%DOWNLOAD_FILEPATH%\"");
            stream.WriteLine("call :DeleteIfSafe \"%FILEPATH%\"");
            stream.WriteLine(
                "if defined INSTALL_WORKSPACE_PATH if exist \"%INSTALL_WORKSPACE_PATH%\" rmdir /S /Q \"%INSTALL_WORKSPACE_PATH%\"");
            stream.WriteLine(
                "if defined DOWNLOAD_WORKSPACE_PATH if exist \"%DOWNLOAD_WORKSPACE_PATH%\" rmdir /S /Q \"%DOWNLOAD_WORKSPACE_PATH%\"");
            stream.WriteLine();


            stream.WriteLine($"if \"%{nameof(EntryApplication.AssemblyConfiguration)}%\"==\"Release\" (");
            stream.WriteLine("  start \"\" /b cmd /c \"timeout /t 1 >nul & del /F /Q \"\"%~f0\"\" >nul 2>&1\"");
            stream.WriteLine(")");
            stream.WriteLine();

            stream.WriteLine("endlocal");
            stream.WriteLine("echo - Completed");
            stream.WriteLine("REM End of script");
            stream.WriteLine("exit /b 0");
            stream.WriteLine();

            stream.WriteLine("REM ----------------------------------------");
            stream.WriteLine("REM Usage:");
            stream.WriteLine("REM   call :DeleteIfSafe \"C:\\path\\to\\file.tmp\" \"Removing temp file\"");
            stream.WriteLine("REM Returns:");
            stream.WriteLine("REM   0 = deleted OR not found");
            stream.WriteLine("REM   1 = unsafe path OR failed to delete");
            stream.WriteLine("REM ----------------------------------------");
            stream.WriteLine(":DeleteIfSafe");
            stream.WriteLine("setlocal EnableExtensions");
            stream.WriteLine("set \"FILE=%~1\"");
            stream.WriteLine("set \"MSG=%~2\"");
            stream.WriteLine();
            stream.WriteLine("if defined MSG echo(%MSG%");
            stream.WriteLine();
            stream.WriteLine("REM Safety: empty");
            stream.WriteLine("if not defined FILE (");
            stream.WriteLine("  echo(  [SKIP] Empty path");
            stream.WriteLine("  exit /b 1");
            stream.WriteLine(")");
            stream.WriteLine();
            stream.WriteLine("REM Safety: \"\\\"");
            stream.WriteLine("if \"%FILE%\"==\"\\\" (");
            stream.WriteLine("  echo(  [SKIP] Unsafe path: \"\\\"");
            stream.WriteLine("  exit /b 1");
            stream.WriteLine(")");
            stream.WriteLine();
            stream.WriteLine("REM Safety: block drive root like C:\\, D:\\");
            stream.WriteLine("if \"%FILE:~1,2%\"==\":\\\" if \"%FILE:~3%\"==\"\" (");
            stream.WriteLine("  echo(  [SKIP] Unsafe path: drive root \"%FILE%\"");
            stream.WriteLine("  exit /b 1");
            stream.WriteLine(")");
            stream.WriteLine();
            stream.WriteLine("REM Not found = not an error (change to exit /b 1 if you prefer)");
            stream.WriteLine("if not exist \"%FILE%\" (");
            stream.WriteLine("  REM echo(  [SKIP] Not found \"%FILE%\"");
            stream.WriteLine("  exit /b 0");
            stream.WriteLine(")");
            stream.WriteLine();
            stream.WriteLine("del /F /Q \"%FILE%\" >nul 2>&1");
            stream.WriteLine(" REM /F - Force deleting of read-only files.");
            stream.WriteLine(" REM /Q - Quiet mode, do not ask if ok to delete on global wildcard.");
            stream.WriteLine();
            stream.WriteLine("if exist \"%FILE%\" (");
            stream.WriteLine("  echo(  [FAIL] Could not delete \"%FILE%\"");
            stream.WriteLine("  exit /b 1");
            stream.WriteLine(")");
            stream.WriteLine();
            stream.WriteLine("echo(  [OK] Deleted \"%FILE%\"");
            stream.WriteLine("exit /b 0");
            stream.WriteLine("REM End of :DeleteIfSafe subroutine");
        }

        // ***********
        // ** Linux **
        // ***********
        void WriteLinuxScriptHeader(StreamWriter stream)
        {
            // Shebang line
            stream.WriteLine("#!/usr/bin/env bash");
            stream.WriteLine(
                $"# Autogenerated by {nameof(UpdatumManager)} v{LibraryVersion.ToString(3)} [{DateTime.Now}]");
            stream.WriteLine($"# {EntryApplication.AssemblyName} upgrade script");
            stream.WriteLine(
                $"echo \"{EntryApplication.AssemblyName} v{currentVersion} -> {newVersionStr} updater script\"");
            stream.WriteLine("cd \"$(dirname \"$0\")\"");
            stream.WriteLine();

            // Set EntryApplication variables
            stream.WriteLine($"# {nameof(EntryApplication)} variables");
            var info = EntryApplication.GetApplicationInfoDict();
            foreach (var kp in info)
            {
                stream.WriteLine($"{kp.Key}={kp.Value.QuoteBashAnsiCString()}");
            }

            stream.WriteLine();

            // Set variables
            stream.WriteLine("# Variables");
            stream.WriteLine($"oldVersion={EntryApplication.AssemblyVersionString.QuoteBashAnsiCString()}");
            stream.WriteLine($"newVersion={newVersionStr.QuoteBashAnsiCString()}");
            stream.WriteLine($"DOWNLOAD_FILEPATH={downloadedAsset.FilePath.QuoteBashAnsiCString()}");
            stream.WriteLine(
                $"DOWNLOAD_WORKSPACE_PATH={downloadedAsset.TemporaryDirectoryPath.QuoteBashAnsiCString()}");
            stream.WriteLine($"INSTALL_WORKSPACE_PATH={installWorkspacePath.QuoteBashAnsiCString()}");
            stream.WriteLine($"FILEPATH={filePath.QuoteBashAnsiCString()}");
            stream.WriteLine(
                $"RUN_AFTER_UPGRADE={(runArguments != NoRunAfterUpgradeToken).ToString().QuoteBashAnsiCString()}");
            stream.WriteLine($"RUN_ARGUMENTS={runArguments.QuoteBashAnsiCString()}");
            stream.WriteLine();

            // Functions
            stream.WriteLine("# ----------------------------------------");
            stream.WriteLine("# Usage:");
            stream.WriteLine("#   delete_if_safe \"/path/to/file.tmp\" \"Removing temp file\"");
            stream.WriteLine("# Returns:");
            stream.WriteLine("#   0 = deleted OR not found");
            stream.WriteLine("#   1 = unsafe path OR failed to delete");
            stream.WriteLine("# ----------------------------------------");
            stream.WriteLine("delete_if_safe() {");
            stream.WriteLine("  local FILE=\"${1-}\"");
            stream.WriteLine("  local MSG=\"${2-}\"");
            stream.WriteLine();
            stream.WriteLine("  # Print message");
            stream.WriteLine("  if [[ -n \"$MSG\" ]]; then");
            stream.WriteLine("    echo \"$MSG\"");
            stream.WriteLine("  fi");
            stream.WriteLine();
            stream.WriteLine("  # Safety: empty");
            stream.WriteLine("  if [[ -z \"$FILE\" ]]; then");
            stream.WriteLine("    echo \"  [SKIP] Empty path\"");
            stream.WriteLine("    return 1");
            stream.WriteLine("  fi");
            stream.WriteLine();
            stream.WriteLine("  # Safety: root");
            stream.WriteLine("  if [[ \"$FILE\" == \"/\" ]]; then");
            stream.WriteLine("    echo \"  [SKIP] Unsafe path: \\\"/\\\"\"");
            stream.WriteLine("    return 1");
            stream.WriteLine("  fi");
            stream.WriteLine();
            stream.WriteLine("  # Safety: also block \".\" and \"..\" (optional but sensible)");
            stream.WriteLine("  if [[ \"$FILE\" == \".\" || \"$FILE\" == \"..\" ]]; then");
            stream.WriteLine("    echo \"  [SKIP] Unsafe path: \\\"$FILE\\\"\"");
            stream.WriteLine("    return 1");
            stream.WriteLine("  fi");
            stream.WriteLine();
            stream.WriteLine("  # Not found = not an error");
            stream.WriteLine("  if [[ ! -e \"$FILE\" ]]; then");
            stream.WriteLine("    # echo \"  [SKIP] Not found \\\"$FILE\\\"\"");
            stream.WriteLine("    return 0");
            stream.WriteLine("  fi");
            stream.WriteLine();
            stream.WriteLine("  # Delete (force, no prompt)");
            stream.WriteLine("  rm -f -- \"$FILE\" 2>/dev/null");
            stream.WriteLine();
            stream.WriteLine("  # Verify");
            stream.WriteLine("  if [[ -e \"$FILE\" ]]; then");
            stream.WriteLine("    echo \"  [FAIL] Could not delete \\\"$FILE\\\"\"");
            stream.WriteLine("    return 1");
            stream.WriteLine("  fi");
            stream.WriteLine();
            stream.WriteLine("  echo \"  [OK] Deleted \\\"$FILE\\\"\"");
            stream.WriteLine("  return 0");
            stream.WriteLine("}");
            stream.WriteLine("# End of delete_if_safe()");
            stream.WriteLine();
        }

        void WriteLinuxScriptKillInstances(StreamWriter stream)
        {
            var killCommands = new List<string>(3);

            if (EntryApplication.IsRunningFromDotNetProcess)
            {
                // Dangerous if script run again latter.
                //stream.WriteLine($"kill -9 {Environment.ProcessId}");
            }
            else if (!string.IsNullOrWhiteSpace(EntryApplication.ProcessName))
            {
                killCommands.Add($"-x \"${nameof(EntryApplication.ProcessName)}\"");
            }

            if (!string.IsNullOrWhiteSpace(EntryApplication.AssemblyName))
            {
                if (killCommands.Count == 0 || EntryApplication.ProcessName != EntryApplication.AssemblyName)
                {
                    killCommands.Add($"-f \"${nameof(EntryApplication.AssemblyName)}\"");
                }
            }

            if (!string.IsNullOrWhiteSpace(EntryApplication.AssemblyLocation))
            {
                killCommands.Add($"-f \"dotnet.+{Regex.Escape(Path.GetFileName(EntryApplication.AssemblyLocation))}\"");
            }

            if (killCommands.Count > 0)
            {
                // Kill processes
                stream.WriteLine("echo \"- Killing processes\"");
                stream.WriteLine("sleep 0.5");
                stream.WriteLine();

                foreach (var killCommand in killCommands)
                {
                    stream.WriteLine($"pkill -TERM {killCommand} || true");
                }

                stream.WriteLine("sleep 2");
                foreach (var killCommand in killCommands)
                {
                    stream.WriteLine($"pkill -KILL {killCommand} || true");
                }

                stream.WriteLine("sleep 0.5");
                stream.WriteLine();
            }
        }

        void WriteLinuxScriptInjectCustomScript(StreamWriter stream)
        {
            if (!string.IsNullOrWhiteSpace(InstallUpdateInjectCustomScript))
            {
                stream.WriteLine("# Custom script provided by the author.");
                // Ensure only LF is used on Linux
                stream.WriteLine(InstallUpdateInjectCustomScript.Replace("\r\n", "\n").Replace("\r", "\n"));
                stream.WriteLine("# End of custom script provided by the author.");
                stream.WriteLine();
            }
        }

        void WriteLinuxScriptEnd(StreamWriter stream)
        {
            stream.WriteLine("echo \"- Removing temp source files\"");

            stream.WriteLine("delete_if_safe \"$DOWNLOAD_FILEPATH\"");
            stream.WriteLine("delete_if_safe \"$FILEPATH\"");
            stream.WriteLine(
                "[[ -n \"$INSTALL_WORKSPACE_PATH\" && \"$INSTALL_WORKSPACE_PATH\" != \"/\" ]] && rm -rf -- \"$INSTALL_WORKSPACE_PATH\"");
            stream.WriteLine(
                "[[ -n \"$DOWNLOAD_WORKSPACE_PATH\" && \"$DOWNLOAD_WORKSPACE_PATH\" != \"/\" ]] && rm -rf -- \"$DOWNLOAD_WORKSPACE_PATH\"");
            stream.WriteLine();

            stream.WriteLine($"if [ \"${nameof(EntryApplication.AssemblyConfiguration)}\" = \"Release\" ]; then");
            stream.WriteLine("  delete_if_safe \"$0\" \"- Removing self\"");
            stream.WriteLine("fi");
            stream.WriteLine();


            stream.WriteLine("echo \"- Completed\"");
            stream.WriteLine("# End of script");
        }

        return Task.Run(async () =>
        {
            var cleanupTransferredToScript = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ///////////////////////////////////////////////////////////
                // This can be a portable, app or single file executable //
                // If single file extract it and use it instead          //
                ///////////////////////////////////////////////////////////
                if (fileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var archive = ZipFile.OpenRead(filePath);

                    if (archive.Entries.Count == 0) return false; // No entries in the archive
                    if (archive.Entries.Count == 1 && archive.Entries[0].IsFile())
                    {
                        var entry = archive.Entries[0];
                        filePath = Path.Combine(installWorkspacePath, entry.Name);
                        entry.ExtractToFile(filePath, false);

                        // Replace downloaded file with the extracted file and process next checks
                        fileName = Path.GetFileName(filePath);
                        fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
                        fileExtension = Path.GetExtension(fileName);
                    }
                    else // Portable app
                    {
                        // Extract the archive to a temporary directory
                        var extractDirectoryPath = Path.Combine(installWorkspacePath, $"extracted-{Guid.NewGuid():N}");
                        Directory.CreateDirectory(extractDirectoryPath);
                        archive.ExtractToDirectory(extractDirectoryPath, false);

                        var targetDirectoryPath = EntryApplication.BaseDirectory;
                        var currentExecutablePath = EntryApplication.ExecutablePath;
                        var newExecutingFilePath = currentExecutablePath;

                        if (string.IsNullOrWhiteSpace(targetDirectoryPath))
                        {
                            targetDirectoryPath = Path.Combine(Utilities.CommonDefaultApplicationDirectory,
                                !string.IsNullOrWhiteSpace(InstallUpdateSingleFileExecutableName)
                                    ? string.Format(InstallUpdateSingleFileExecutableName, newVersionStr)
                                    : EntryApplication.AssemblyName
                                      ?? Path.GetFileNameWithoutExtension(EntryApplication.ExecutableName)
                                      ?? fileNameNoExt);
                        }

                        var di = new DirectoryInfo(targetDirectoryPath);
                        var replacementParentPath = di.Parent?.FullName
                                                    ?? throw new InvalidOperationException(
                                                        "The application directory cannot be a filesystem root.");
                        var replacementId = Guid.NewGuid().ToString("N");
                        var stagedDirectoryPath = Path.Combine(replacementParentPath,
                            $".{di.Name}.updatum-new-{replacementId}");
                        var backupDirectoryPath = Path.Combine(replacementParentPath,
                            $".{di.Name}.updatum-backup-{replacementId}");

                        if (OperatingSystem.IsWindows())
                        {
                            string upgradeScriptFilePath;
                            using (var temporaryScriptFile =
                                   CreateScriptFile(out upgradeScriptFilePath, out var stream))
                            using (stream)
                            {
                                WriteWindowsScriptHeader(stream);

                                stream.WriteLine(
                                    $"set \"SOURCE_PATH={extractDirectoryPath.EscapeWindowsBatchValue()}\"");
                                stream.WriteLine(
                                    $"set \"DEST_PATH={targetDirectoryPath.EscapeWindowsBatchValue()}\"");
                                stream.WriteLine(
                                    $"set \"STAGED_PATH={stagedDirectoryPath.EscapeWindowsBatchValue()}\"");
                                stream.WriteLine(
                                    $"set \"BACKUP_PATH={backupDirectoryPath.EscapeWindowsBatchValue()}\"");
                                stream.WriteLine();

                                // Source path verification
                                stream.WriteLine("if not exist \"%SOURCE_PATH%\" (");
                                stream.WriteLine("  echo - Error: Source path does not exist");
                                stream.WriteLine("  exit /b 1");
                                stream.WriteLine(')');
                                stream.WriteLine();

                                if (forceTerminate) WriteWindowsScriptKillInstances(stream);
                                UpdatumInstallScript.WriteWindowsDirectoryReplacement(stream);

                                // Replace folder name with the new version name when required
                                if (Version.TryParse(newVersionStr, out var newVersion) &&
                                    !currentVersion.Equals(newVersion))
                                {
                                    var newDirectoryName = SanitizeDirectoryNameWithVersion(di.Name, newVersionStr);
                                    if (di.Name != newDirectoryName)
                                    {
                                        var parent = di.Parent;
                                        if (parent is not null)
                                        {
                                            stream.WriteLine("echo - Directory is able to rename version name");
                                            var newTargetDirectoryPath =
                                                Path.Combine(parent.FullName, newDirectoryName);
                                            stream.WriteLine(
                                                $"if exist \"{newTargetDirectoryPath.EscapeWindowsBatchValue()}\" (");
                                            stream.WriteLine(
                                                "  echo - Could not rename directory: target already exists");
                                            stream.WriteLine(") else (");
                                            stream.WriteLine("  echo - Attempt to rename directory");
                                            stream.WriteLine(
                                                $"  move /Y \"%DEST_PATH%\" \"{newTargetDirectoryPath.EscapeWindowsBatchValue()}\" >nul");
                                            stream.WriteLine("  if errorlevel 1 (");
                                            stream.WriteLine(
                                                "    echo - Could not rename directory; continuing with the original path");
                                            stream.WriteLine("  ) else (");
                                            stream.WriteLine(
                                                $"    set \"{nameof(EntryApplication.BaseDirectory)}={newTargetDirectoryPath.EscapeWindowsBatchValue()}\"");
                                            stream.WriteLine(
                                                $"    set \"DEST_PATH=%{nameof(EntryApplication.BaseDirectory)}%\"");

                                            if (!string.IsNullOrWhiteSpace(newExecutingFilePath))
                                            {
                                                newExecutingFilePath =
                                                    newExecutingFilePath.Replace(di.FullName, newTargetDirectoryPath);
                                                stream.WriteLine(
                                                    $"    set \"{nameof(EntryApplication.ExecutablePath)}={newExecutingFilePath.EscapeWindowsBatchValue()}\"");
                                            }

                                            stream.WriteLine("  )");
                                            stream.WriteLine(")");
                                            stream.WriteLine();
                                        }
                                    }
                                }

                                WriteWindowsScriptInjectCustomScript(stream);

                                stream.WriteLine(
                                    $"if not \"%{nameof(EntryApplication.ExecutablePath)}%\"==\"\" if /I \"%RUN_AFTER_UPGRADE%\"==\"True\" (");
                                stream.WriteLine($"  echo - Execute the upgraded application");
                                stream.WriteLine($"  if exist \"%{nameof(EntryApplication.ExecutablePath)}%\" (");
                                stream.WriteLine(EntryApplication.IsRunningFromDotNetProcess
                                    ? $"    start \"\" \"{Environment.ProcessPath}\" \"%{nameof(EntryApplication.ExecutablePath)}%\" %RUN_ARGUMENTS%"
                                    : $"    start \"\" \"%{nameof(EntryApplication.ExecutablePath)}%\" %RUN_ARGUMENTS%");
                                stream.WriteLine("  ) else (");
                                stream.WriteLine(
                                    $"    echo File not found: %{nameof(EntryApplication.ExecutablePath)}%, not executing!");
                                stream.WriteLine("  )");
                                stream.WriteLine(") else (");
                                stream.WriteLine(
                                    "  echo - Skip execution of application, by the configuration or unable to locate the entry point");
                                stream.WriteLine(")");

                                stream.WriteLine();

                                WriteWindowsScriptEnd(stream);
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            RaiseEvent(InstallUpdateCompleted, downloadedAsset);

                            var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(upgradeScriptFilePath);
                            startInfo.WorkingDirectory = tmpPath;
                            var result = ProcessHelper.StartProcess(startInfo);

                            if (result != 0) return false;

                            cleanupTransferredToScript = true;
                        }
                        else // Linux or macOS
                        {
                            string upgradeScriptFilePath;
                            using (var temporaryScriptFile =
                                   CreateScriptFile(out upgradeScriptFilePath, out var stream))
                            using (stream)
                            {
                                WriteLinuxScriptHeader(stream);
                                stream.WriteLine($"SOURCE_PATH={extractDirectoryPath.QuoteBashAnsiCString()}");
                                stream.WriteLine($"DEST_PATH={targetDirectoryPath.QuoteBashAnsiCString()}");
                                stream.WriteLine($"STAGED_PATH={stagedDirectoryPath.QuoteBashAnsiCString()}");
                                stream.WriteLine($"BACKUP_PATH={backupDirectoryPath.QuoteBashAnsiCString()}");
                                stream.WriteLine();

                                // Source path verification
                                stream.WriteLine("if [ ! -d \"$SOURCE_PATH\" ]; then");
                                stream.WriteLine("  echo \"- Error: Source path does not exist\"");
                                stream.WriteLine("  exit 1");
                                stream.WriteLine("fi");
                                stream.WriteLine();

                                if (forceTerminate) WriteLinuxScriptKillInstances(stream);


                                if (OperatingSystem.IsMacOS())
                                {
                                    stream.WriteLine("echo \"- Removing com.apple.quarantine flag\"");
                                    stream.WriteLine(
                                        "find \"$SOURCE_PATH\" -print0 | xargs -0 xattr -d com.apple.quarantine &> /dev/null || true");
                                    stream.WriteLine();

                                    if (InstallUpdateCodesignMacOSApp)
                                    {
                                        stream.WriteLine(
                                            "echo \"- Force codesign to allow the app to run directly\"");
                                        stream.WriteLine(
                                            "find \"$SOURCE_PATH\" -maxdepth 1 -type d -name \"*.app\" -print0 | xargs -0 -I {} codesign --force --deep --sign - \"{}\" || true");
                                        stream.WriteLine();
                                    }
                                }

                                UpdatumInstallScript.WriteUnixDirectoryReplacement(stream);


                                // Replace folder name with the new version name when required
                                if (Version.TryParse(newVersionStr, out var newVersion) &&
                                    !currentVersion.Equals(newVersion))
                                {
                                    var newDirectoryName = SanitizeDirectoryNameWithVersion(di.Name, newVersionStr);
                                    if (di.Name != newDirectoryName)
                                    {
                                        var parent = di.Parent;
                                        if (parent is not null)
                                        {
                                            stream.WriteLine("echo \"- Directory is able to rename version name\"");
                                            var newTargetDirectoryPath =
                                                Path.Combine(parent.FullName, newDirectoryName);
                                            stream.WriteLine(
                                                $"NEW_DEST_PATH={newTargetDirectoryPath.QuoteBashAnsiCString()}");
                                            stream.WriteLine("if [[ -e \"$NEW_DEST_PATH\" ]]; then");
                                            stream.WriteLine(
                                                "  echo \"- Could not rename directory: target already exists\"");
                                            stream.WriteLine("elif mv -- \"$DEST_PATH\" \"$NEW_DEST_PATH\"; then");
                                            stream.WriteLine(
                                                $"  {nameof(EntryApplication.BaseDirectory)}=\"$NEW_DEST_PATH\"");
                                            stream.WriteLine(
                                                $"  DEST_PATH=\"${nameof(EntryApplication.BaseDirectory)}\"");

                                            if (!string.IsNullOrWhiteSpace(newExecutingFilePath))
                                            {
                                                newExecutingFilePath =
                                                    newExecutingFilePath.Replace(di.FullName, newTargetDirectoryPath);
                                                stream.WriteLine(
                                                    $"  {nameof(EntryApplication.ExecutablePath)}={newExecutingFilePath.QuoteBashAnsiCString()}");
                                            }

                                            stream.WriteLine("else");
                                            stream.WriteLine(
                                                "  echo \"- Could not rename directory; continuing with the original path\"");
                                            stream.WriteLine("fi");
                                            stream.WriteLine();
                                        }
                                    }
                                }

                                // Custom script injection
                                WriteLinuxScriptInjectCustomScript(stream);

                                // Execute the upgraded application
                                stream.WriteLine(
                                    $"if [ -n \"${nameof(EntryApplication.ExecutablePath)}\" ] && [ \"${{RUN_AFTER_UPGRADE:-False}}\" = \"True\" ]; then");
                                stream.WriteLine("  echo \"- Execute the upgraded application\"");
                                stream.WriteLine($"  if [ -f \"${nameof(EntryApplication.ExecutablePath)}\" ]; then");
                                if (EntryApplication.IsRunningFromDotNetProcess)
                                {
                                    stream.WriteLine(
                                        $"    nohup \"{Environment.ProcessPath}\" \"${nameof(EntryApplication.ExecutablePath)}\" $RUN_ARGUMENTS >/dev/null 2>&1 &");
                                }
                                else
                                {
                                    // Make executable if it's not
                                    stream.WriteLine($"    chmod +x \"${nameof(EntryApplication.ExecutablePath)}\"");
                                    stream.WriteLine(
                                        $"    nohup \"${nameof(EntryApplication.ExecutablePath)}\" $RUN_ARGUMENTS >/dev/null 2>&1 &");
                                }

                                stream.WriteLine("    sleep 1"); // Let the process start
                                stream.WriteLine("    if ps -p $! >/dev/null; then");
                                stream.WriteLine("      echo \"- Success: Application running (PID: $!)\"");
                                stream.WriteLine("    else");
                                stream.WriteLine("      echo \"- Error: Process failed to start\"");
                                stream.WriteLine("    fi");
                                stream.WriteLine("  else");
                                stream.WriteLine(
                                    $"    echo \"- File not found: ${nameof(EntryApplication.ExecutablePath)}, not executing!\"");
                                stream.WriteLine("  fi");
                                stream.WriteLine("else");
                                stream.WriteLine(
                                    "  echo \"- Skip execution of application (RUN_AFTER_UPGRADE is not true).\"");
                                stream.WriteLine("fi");
                                stream.WriteLine();

                                WriteLinuxScriptEnd(stream);
                            }

                            // Make the script executable
                            UnixSystem.SetUnix755Executable(upgradeScriptFilePath);

                            cancellationToken.ThrowIfCancellationRequested();
                            RaiseEvent(InstallUpdateCompleted, downloadedAsset);

                            // Execute the script
                            var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(upgradeScriptFilePath);
                            startInfo.WorkingDirectory = tmpPath;
                            var result = ProcessHelper.StartProcess(startInfo);

                            if (result != 0) return false;
                            cleanupTransferredToScript = true;
                        }

                        if (forceTerminate) Environment.Exit(0);
                        return true;
                    }
                }

                var inferredPackageType = GetPackagingTypeForFile(filePath);

                ////////////////////////
                // Windows Installers //
                ////////////////////////
                if (OperatingSystem.IsWindows())
                {
                    var isWindowsInstaller = false;
                    if (fileExtension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        isWindowsInstaller = true;
                    }
                    else if (fileExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (InstallUpdateWindowsExeType)
                        {
                            case UpdatumWindowsExeType.Auto:
                                isWindowsInstaller = Utilities.IsWindowsInstallerFile(filePath);
                                break;
                            case UpdatumWindowsExeType.Installer:
                                isWindowsInstaller = true;
                                break;
                            case UpdatumWindowsExeType.SingleFileApp:
                                break;
                            default:
                                throw new InvalidOperationException(
                                    $"Unexpected value for {nameof(InstallUpdateWindowsExeType)}: {InstallUpdateWindowsExeType}");
                        }
                    }

                    if (isWindowsInstaller)
                    {
                        string upgradeScriptFilePath;
                        using (var temporaryScriptFile = CreateScriptFile(out upgradeScriptFilePath, out var stream))
                        using (stream)
                        {
                            WriteWindowsScriptHeader(stream);
                            WriteWindowsScriptFileValidation(stream);
                            if (forceTerminate) WriteWindowsScriptKillInstances(stream);
                            WriteWindowsScriptInjectCustomScript(stream);

                            stream.WriteLine("echo - Calling the installer");
                            stream.WriteLine(
                                $"start \"\" /WAIT \"%FILEPATH%\" {InstallUpdateWindowsInstallerArguments}");
                            stream.WriteLine(" REM /WAIT - Start application and wait for it to terminate.");
                            stream.WriteLine();

                            stream.WriteLine("if /I \"%RUN_AFTER_UPGRADE%\"==\"True\" (");
                            stream.WriteLine("  echo - Execute the upgraded application");
                            stream.WriteLine($"  if exist \"%{nameof(EntryApplication.ExecutablePath)}%\" (");
                            stream.WriteLine(
                                $"    start \"\" \"%{nameof(EntryApplication.ExecutablePath)}%\" %RUN_ARGUMENTS%");
                            stream.WriteLine("  ) else (");
                            stream.WriteLine(
                                $"    echo - File not found: \"%{nameof(EntryApplication.ExecutablePath)}%\", not executing!");
                            stream.WriteLine("  )");
                            stream.WriteLine(") else (");
                            stream.WriteLine(
                                "  echo - Skip execution of application (RUN_AFTER_UPGRADE is not true)");
                            stream.WriteLine(")");
                            stream.WriteLine();


                            WriteWindowsScriptEnd(stream);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        RaiseEvent(InstallUpdateCompleted, downloadedAsset);

                        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(upgradeScriptFilePath);
                        startInfo.WorkingDirectory = tmpPath;
                        var result = ProcessHelper.StartProcess(startInfo);

                        if (result != 0) return false;
                        cleanupTransferredToScript = true;

                        if (forceTerminate) Environment.Exit(0); // Exit the application to install
                        return true;
                    }
                }

                ///////////////////////////
                // macOS package formats //
                ///////////////////////////
                if (inferredPackageType is ApplicationPackagingType.MacOSPkg or ApplicationPackagingType.MacOSDmg)
                {
                    if (!OperatingSystem.IsMacOS())
                        throw new NotSupportedException($"The package '{fileName}' is only supported on macOS.");

                    var isPkgPackage = inferredPackageType is ApplicationPackagingType.MacOSPkg;

                    using var temporaryScriptFile = new TemporaryFile(installWorkspacePath, ".sh");
                    var installScriptFilePath = temporaryScriptFile.FilePath;
                    using (var stream = new StreamWriter(temporaryScriptFile.Create()))
                    {
                        stream.NewLine = "\n";
                        WriteLinuxScriptHeader(stream);
                        stream.WriteLine(
                            $"CURRENT_APP_BUNDLE_PATH={EntryApplication.MacOSAppBundlePath.QuoteBashAnsiCString()}");
                        stream.WriteLine(
                            $"MACOS_CODESIGN_APP={InstallUpdateCodesignMacOSApp.ToString().QuoteBashAnsiCString()}");
                        stream.WriteLine();

                        // Mount and resolve the target before anything with side effects, so a privilege-escalation
                        // retry never runs the custom script twice.
                        if (!isPkgPackage) UpdatumInstallScript.WriteMacOSDmgPreparation(stream);

                        WriteLinuxScriptInjectCustomScript(stream);

                        if (isPkgPackage) UpdatumInstallScript.WriteMacOSPkgInstallation(stream);
                        else UpdatumInstallScript.WriteMacOSDmgInstallation(stream);
                    }

                    UnixSystem.SetUnix755Executable(installScriptFilePath);
                    cancellationToken.ThrowIfCancellationRequested();

                    // A PKG always installs into the system domain and needs root. A DMG only needs it when it wraps
                    // a PKG or targets a directory the user cannot write, so try unprivileged first and let the
                    // script ask for elevation.
                    var exitCode = await RunMacOSInstallScriptAsync(
                            installScriptFilePath,
                            installWorkspacePath,
                            isPkgPackage,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (!isPkgPackage && exitCode == UpdatumInstallScript.PrivilegeEscalationRequiredExitCode)
                    {
                        exitCode = await RunMacOSInstallScriptAsync(
                                installScriptFilePath,
                                installWorkspacePath,
                                true,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    EnsurePackageInstallationSucceeded(isPkgPackage ? "macOS PKG" : "macOS DMG", exitCode);

                    cancellationToken.ThrowIfCancellationRequested();
                    RaiseEvent(InstallUpdateCompleted, downloadedAsset);
                    if (runArguments != NoRunAfterUpgradeToken)
                    {
                        if (forceTerminate)
                        {
                            var appBundlePath = EntryApplication.MacOSAppBundlePath;
                            if (string.IsNullOrWhiteSpace(appBundlePath))
                            {
                                throw new IOException(
                                    "The installed macOS application bundle path could not be determined for relaunch.");
                            }

                            var relaunchStartInfo = UpdatumInstallScript.CreateMacOSRelaunchProcessStartInfo(
                                Environment.ProcessId,
                                appBundlePath,
                                runArguments);
                            if (ProcessHelper.StartProcess(relaunchStartInfo) != 0)
                                throw new IOException("The macOS application relaunch helper could not be started.");
                        }
                        else
                        {
                            EntryApplication.LaunchNewInstance(runArguments);
                        }
                    }

                    downloadedAsset.SafeDeleteFile();
                    if (forceTerminate)
                    {
                        // Environment.Exit runs neither the using above nor the finally below, and the script has
                        // already completed, so release the workspace explicitly.
                        temporaryScriptFile.Dispose();
                        DeleteTemporaryWorkspace(installWorkspacePath);
                        Environment.Exit(0);
                    }

                    return true;
                }

                ///////////////////////////
                // Linux package formats //
                ///////////////////////////
                if (inferredPackageType
                    is ApplicationPackagingType.LinuxFlatpak
                    or ApplicationPackagingType.LinuxSnap
                    or ApplicationPackagingType.LinuxDeb
                    or ApplicationPackagingType.LinuxRpm
                    or ApplicationPackagingType.LinuxArchPackage)
                {
                    if (!OperatingSystem.IsLinux())
                        throw new NotSupportedException($"The package '{fileName}' is only supported on Linux.");

                    var flatpakInstallationScope = FlatpakInstallationScope.User;
                    string? resolvedFlatpakExecutable = null;
                    if (inferredPackageType is ApplicationPackagingType.LinuxFlatpak)
                    {
                        var initialCommand = CreateLinuxPackageInstallCommand(
                            filePath,
                            LinuxRuntime.PackageManager,
                            flatpakInstallationScope);
                        resolvedFlatpakExecutable = ResolvePackageInstallerExecutable(initialCommand);
                        flatpakInstallationScope = await GetFlatpakInstallationScopeAsync(
                                resolvedFlatpakExecutable,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var installCommand = CreateLinuxPackageInstallCommand(
                        filePath,
                        LinuxRuntime.PackageManager,
                        flatpakInstallationScope);
                    var installerExecutable = resolvedFlatpakExecutable
                                              ?? ResolvePackageInstallerExecutable(installCommand);
                    var exitCode = inferredPackageType is ApplicationPackagingType.LinuxFlatpak
                        ? await ProcessHelper.StartHostProcessAsync(
                                installerExecutable,
                                installCommand.Arguments,
                                installCommand.RequiresElevation,
                                true,
                                PackageInstallTimeoutMilliseconds,
                                cancellationToken)
                            .ConfigureAwait(false)
                        : await ProcessHelper.StartProcessAsync(
                                installerExecutable,
                                installCommand.Arguments,
                                installCommand.RequiresElevation,
                                true,
                                PackageInstallTimeoutMilliseconds,
                                cancellationToken)
                            .ConfigureAwait(false);

                    EnsurePackageInstallationSucceeded(installCommand.PackageType, exitCode);

                    cancellationToken.ThrowIfCancellationRequested();
                    RaiseEvent(InstallUpdateCompleted, downloadedAsset);
                    if (runArguments != NoRunAfterUpgradeToken) StartUpdatedLinuxPackage(filePath, runArguments);

                    downloadedAsset.SafeDeleteFile();
                    if (forceTerminate) Environment.Exit(0);
                    return true;
                }

                ///////////////////////////////////////////////////////////
                // Handle single-file apps / executables for all systems //
                ///////////////////////////////////////////////////////////
                if (inferredPackageType
                    is ApplicationPackagingType.DotNetSingleFile
                    or ApplicationPackagingType.LinuxAppImage)
                {
                    if (inferredPackageType is ApplicationPackagingType.DotNetSingleFile &&
                        OperatingSystem.IsWindows() &&
                        !filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException(
                            "An extensionless single-file update is only supported on Unix systems.");
                    if (inferredPackageType is ApplicationPackagingType.DotNetSingleFile &&
                        filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows())
                        throw new NotSupportedException(
                            "A Windows single-file update is only supported on Windows.");
                    if (inferredPackageType is ApplicationPackagingType.LinuxAppImage && !OperatingSystem.IsLinux())
                        throw new NotSupportedException("An AppImage update is only supported on Linux.");


                    var currentExecutablePath = EntryApplication.ExecutablePath;
                    var targetDirectoryPath = EntryApplication.BaseDirectory;

                    if (string.IsNullOrWhiteSpace(targetDirectoryPath))
                    {
                        if (OperatingSystem.IsLinux())
                        {
                            targetDirectoryPath = Utilities.LinuxDefaultApplicationDirectory;
                            Directory.CreateDirectory(targetDirectoryPath);
                        }
                        else if (OperatingSystem.IsMacOS())
                        {
                            targetDirectoryPath = Utilities.MacOSDefaultApplicationDirectory;
                        }
                        else
                        {
                            targetDirectoryPath = Utilities.CommonDefaultApplicationDirectory;
                        }
                    }

                    // By defaults uses same filename as currently downloaded
                    var targetFileName = fileNameNoExt;
                    var currentExecutableFileName = Path.GetFileName(currentExecutablePath);

                    // Infer from executing filename and use it if the first 3 characters are the same,
                    // assume it's the same base name and just change the version part
                    bool SetTargetNameFromCurrentExecutableName()
                    {
                        if (!string.IsNullOrWhiteSpace(currentExecutablePath)
                            && !string.IsNullOrWhiteSpace(currentExecutableFileName)
                            && !string.IsNullOrWhiteSpace(targetFileName)
                            && currentExecutableFileName.Length >= 3 && targetFileName.Length >= 3
                            && currentExecutableFileName[0] == targetFileName[0]
                            && currentExecutableFileName[1] == targetFileName[1]
                            && currentExecutableFileName[2] == targetFileName[2])
                        {
                            targetFileName =
                                Path.GetFileNameWithoutExtension(
                                    SanitizeFileNameWithVersion(currentExecutableFileName, newVersionStr));

                            return true;
                        }

                        return false;
                    }

                    // Custom filename
                    bool SetTargetNameWithCustomName()
                    {
                        if (!string.IsNullOrWhiteSpace(InstallUpdateSingleFileExecutableName))
                        {
                            targetFileName = string.Format(InstallUpdateSingleFileExecutableName, newVersionStr);
                            return true;
                        }

                        return false;
                    }


                    switch (InstallUpdateSingleFileExecutableNameStrategy)
                    {
                        case UpdatumSingleFileExecutableNameStrategy.EntryApplicationName:
                            if (!SetTargetNameFromCurrentExecutableName()) SetTargetNameWithCustomName();
                            break;
                        case UpdatumSingleFileExecutableNameStrategy.CustomName:
                            if (!SetTargetNameWithCustomName()) SetTargetNameFromCurrentExecutableName();
                            break;
                        case UpdatumSingleFileExecutableNameStrategy.DownloadName:
                            // Default behavior, already filled as, do nothing
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"Unexpected value for {nameof(InstallUpdateSingleFileExecutableNameStrategy)}: {InstallUpdateSingleFileExecutableNameStrategy}");
                    }

                    var targetFilePath = Path.Combine(targetDirectoryPath, $"{targetFileName}{fileExtension}");
                    var currentFilePath = string.IsNullOrWhiteSpace(currentExecutablePath)
                        ? targetFilePath
                        : currentExecutablePath;
                    var replacementId = Guid.NewGuid().ToString("N");
                    var stagedFilePath = Path.Combine(targetDirectoryPath,
                        $".{Path.GetFileName(targetFilePath)}.updatum-new-{replacementId}");
                    var backupFilePath = Path.Combine(targetDirectoryPath,
                        $".{Path.GetFileName(currentFilePath)}.updatum-backup-{replacementId}");

                    /*File.Move(filePath, targetFilePath, true);

                    if (currentExecutablePath != targetFilePath
                        && !string.IsNullOrWhiteSpace(currentExecutablePath)
                        && File.Exists(currentExecutablePath))
                    {
                        try
                        {
                            File.Delete(currentExecutablePath);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    // Set executable permissions for non-windows systems
                    if (!OperatingSystem.IsWindows())
                    {
                        // 755 permissions
                        File.SetUnixFileMode(targetFilePath, Utilities.Unix755FileMode);
                    }

                    InstallUpdateCompleted?.Invoke(this, downloadedAsset);

                    // Execute the new file
                    if (runArguments != NoRunAfterUpgradeToken) Utilities.StartProcess(targetFilePath, runArguments);

                    // Exit the application
                    if (forceTerminate) Environment.Exit(0);
                    */

                    // New WITH script
                    if (OperatingSystem.IsWindows())
                    {
                        string upgradeScriptFilePath;
                        using (var temporaryScriptFile = CreateScriptFile(out upgradeScriptFilePath, out var stream))
                        using (stream)
                        {
                            WriteWindowsScriptHeader(stream);
                            stream.WriteLine($"set \"SOURCE_FILEPATH={filePath.EscapeWindowsBatchValue()}\"");
                            stream.WriteLine($"set \"CURRENT_FILEPATH={currentFilePath.EscapeWindowsBatchValue()}\"");
                            stream.WriteLine($"set \"TARGET_FILEPATH={targetFilePath.EscapeWindowsBatchValue()}\"");
                            stream.WriteLine($"set \"STAGED_FILEPATH={stagedFilePath.EscapeWindowsBatchValue()}\"");
                            stream.WriteLine($"set \"BACKUP_FILEPATH={backupFilePath.EscapeWindowsBatchValue()}\"");
                            stream.WriteLine();

                            // Source path verification
                            stream.WriteLine("if not exist \"%SOURCE_FILEPATH%\" (");
                            stream.WriteLine("  echo - Error: Source file does not exist");
                            stream.WriteLine("  exit /b 1");
                            stream.WriteLine(')');
                            stream.WriteLine();

                            if (forceTerminate) WriteWindowsScriptKillInstances(stream);
                            WriteWindowsScriptInjectCustomScript(stream);
                            UpdatumInstallScript.WriteWindowsFileReplacement(stream);
                            stream.WriteLine();

                            stream.WriteLine($"if /I \"%RUN_AFTER_UPGRADE%\"==\"True\" (");
                            stream.WriteLine($"  echo - Execute the upgraded application");
                            stream.WriteLine($"  if exist \"%TARGET_FILEPATH%\" (");
                            stream.WriteLine($"    start \"\" \"%TARGET_FILEPATH%\" %RUN_ARGUMENTS%");
                            stream.WriteLine($"  ) else (");
                            stream.WriteLine($"    echo - File not found: \"%TARGET_FILEPATH%\", not executing!");
                            stream.WriteLine($"  )");
                            stream.WriteLine($") else (");
                            stream.WriteLine($"  echo - Skip execution of application (RUN_AFTER_UPGRADE is not true)");
                            stream.WriteLine($")");
                            stream.WriteLine();


                            WriteWindowsScriptEnd(stream);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        RaiseEvent(InstallUpdateCompleted, downloadedAsset);

                        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(upgradeScriptFilePath);
                        startInfo.WorkingDirectory = tmpPath;
                        var result = ProcessHelper.StartProcess(startInfo);

                        if (result != 0) return false;

                        cleanupTransferredToScript = true;


                        if (forceTerminate) Environment.Exit(0); // Exit the application to install

                        return true;
                    }
                    else
                    {
                        string upgradeScriptFilePath;
                        using (var temporaryScriptFile = CreateScriptFile(out upgradeScriptFilePath, out var stream))
                        using (stream)
                        {
                            WriteLinuxScriptHeader(stream);
                            stream.WriteLine($"SOURCE_FILEPATH={filePath.QuoteBashAnsiCString()}");
                            stream.WriteLine($"CURRENT_FILEPATH={currentFilePath.QuoteBashAnsiCString()}");
                            stream.WriteLine($"TARGET_FILEPATH={targetFilePath.QuoteBashAnsiCString()}");
                            stream.WriteLine($"STAGED_FILEPATH={stagedFilePath.QuoteBashAnsiCString()}");
                            stream.WriteLine($"BACKUP_FILEPATH={backupFilePath.QuoteBashAnsiCString()}");
                            stream.WriteLine();

                            // Source path verification
                            stream.WriteLine("if [ ! -f \"$SOURCE_FILEPATH\" ]; then");
                            stream.WriteLine("  echo \"- Error: Source filepath does not exist\"");
                            stream.WriteLine("  exit 1");
                            stream.WriteLine("fi");
                            stream.WriteLine();

                            if (forceTerminate) WriteLinuxScriptKillInstances(stream);
                            WriteLinuxScriptInjectCustomScript(stream);
                            stream.WriteLine("mkdir -p \"$(dirname \"$TARGET_FILEPATH\")\"");
                            UpdatumInstallScript.WriteUnixFileReplacement(stream);
                            stream.WriteLine();

                            if (OperatingSystem.IsMacOS())
                            {
                                stream.WriteLine("echo \"- Removing com.apple.quarantine flag\"");
                                stream.WriteLine(
                                    "xattr -d com.apple.quarantine \"$TARGET_FILEPATH\" &> /dev/null || true");
                                stream.WriteLine();

                                if (InstallUpdateCodesignMacOSApp)
                                {
                                    stream.WriteLine("echo \"- Force codesign to allow the app to run directly\"");
                                    stream.WriteLine("codesign --force --deep --sign - \"$TARGET_FILEPATH\" || true");
                                    stream.WriteLine();
                                }
                            }

                            // Execute the upgraded application
                            stream.WriteLine("if [[ \"${RUN_AFTER_UPGRADE:-False}\" = \"True\" ]]; then");
                            stream.WriteLine("  if [[ -f \"$TARGET_FILEPATH\" ]]; then");
                            stream.WriteLine("    echo \"- Execute the upgraded application\"");
                            stream.WriteLine($"    nohup \"$TARGET_FILEPATH\" $RUN_ARGUMENTS >/dev/null 2>&1 &");
                            stream.WriteLine("    sleep 1"); // Let the process start
                            stream.WriteLine("    if ps -p $! >/dev/null; then");
                            stream.WriteLine("      echo \"- Success: Application running (PID: $!)\"");
                            stream.WriteLine("    else");
                            stream.WriteLine("      echo \"- Error: Process failed to start\"");
                            stream.WriteLine("    fi");
                            stream.WriteLine("  else");
                            stream.WriteLine("    echo \"- File not found: $TARGET_FILEPATH, not executing!\"");
                            stream.WriteLine("  fi");
                            stream.WriteLine("else");
                            stream.WriteLine(
                                "  echo \"- Skip execution of application (RUN_AFTER_UPGRADE is not true)\"");
                            stream.WriteLine("fi");
                            stream.WriteLine();

                            WriteLinuxScriptEnd(stream);
                        }

                        // Make the script executable
                        UnixSystem.SetUnix755Executable(upgradeScriptFilePath);

                        cancellationToken.ThrowIfCancellationRequested();
                        RaiseEvent(InstallUpdateCompleted, downloadedAsset);

                        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(upgradeScriptFilePath);
                        startInfo.WorkingDirectory = tmpPath;
                        var result = ProcessHelper.StartProcess(startInfo);

                        if (result != 0) return false;

                        cleanupTransferredToScript = true;

                        if (forceTerminate) Environment.Exit(0); // Exit the application to install

                        return true;
                    }
                }

                // Unable to find a valid file type to install
                return false;
            }
            finally
            {
                if (!cleanupTransferredToScript) DeleteTemporaryWorkspace(installWorkspacePath);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// <p>Sets <see cref="ReleasesAhead"/> lists to the specified release in order to trigger a forced update.</p>
    /// <p>Use this to test your application, for debug purposes or to force a downgrade.</p>
    /// </summary>
    /// <param name="release">The release to set as an update.</param>
    public void ForceTriggerUpdateFromRelease(Release release)
    {
        ReleasesAhead = [release];
        RaiseEvent(UpdateFound);
    }

    /// <summary>
    /// Clears the <see cref="Releases"/> and <see cref="ReleasesAhead"/> lists.
    /// </summary>
    public void Clear()
    {
        Releases = [];
        ReleasesAhead = [];
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Sanitizes a directory name with the new version name if it uses one and remove the hash if present.
    /// </summary>
    /// <param name="directory">The directory to sanitize.</param>
    /// <param name="newVersion">The new version to replace in directory if it uses a version in it.</param>
    /// <returns>The sanitized directory name, without full path</returns>
    public static string SanitizeDirectoryNameWithVersion(string directory, string newVersion)
    {
        var fileName = Path.GetFileName(directory);

        // Check and replace if the filePath has a version in name
        return ExtractVersionRegex().Replace(fileName, newVersion);
    }

    /// <summary>
    /// Sanitizes a file name with the new version name if it uses one and remove the hash if present.
    /// </summary>
    /// <param name="filePath">The filePath to sanitize.</param>
    /// <param name="newVersion">The new version to replace in filePath if it uses a version in it.</param>
    /// <returns>The sanitized file name, without full path</returns>
    public static string SanitizeFileNameWithVersion(string filePath, string newVersion)
    {
        var filePathSpan = filePath.AsSpan();
        var fileNameNoExt = Path.GetFileNameWithoutExtension(filePathSpan);
        var fileExtension = Path.GetExtension(filePathSpan);

        // Check if the filePath has a hash at the end and strip it
        // - AppImages renames with hash when integrated
        var index = fileNameNoExt.LastIndexOf('_');
        if (index > 0 && fileNameNoExt.Length - index >= 32)
        {
            fileNameNoExt = fileNameNoExt[..index];
        }

        var fileName = SanitizeDirectoryNameWithVersion(fileNameNoExt.ToString(), newVersion);
        return $"{fileName}{fileExtension}";
    }

    /// <summary>
    /// Sanitizes a file name with the new version name if it uses one and remove the hash if present.
    /// </summary>
    /// <param name="filePath">The filePath to sanitize.</param>
    /// <param name="newVersion">The new version to replace in filePath if it uses a version in it.</param>
    /// <returns>The sanitized file name, without full path</returns>
    public static string SanitizeFileNameWithVersion(string filePath, Version newVersion)
    {
        return SanitizeFileNameWithVersion(filePath, newVersion.ToString());
    }

    #endregion

    #region Property Changed

    /// <summary>
    /// Called when a property changes.
    /// </summary>
    /// <param name="e"></param>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
    }

    /// <summary>
    /// Raises the property changed event for the specified property.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="field"></param>
    /// <param name="value"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    ///     Notifies listeners that a property value has changed.
    /// </summary>
    /// <param name="propertyName">
    ///     Name of the property used to notify listeners.  This
    ///     value is optional and can be provided automatically when invoked from compilers
    ///     that support <see cref="CallerMemberNameAttribute" />.
    /// </param>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var e = new PropertyChangedEventArgs(propertyName);

        void Raise()
        {
            OnPropertyChanged(e);
            _propertyChanged?.Invoke(this, e);
        }

        var synchronizationContext = EventSynchronizationContext;
        if (synchronizationContext is not null && synchronizationContext != SynchronizationContext.Current)
        {
            synchronizationContext.Post(_ => Raise(), null);
        }
        else
        {
            Raise();
        }
    }

    /// <summary>
    /// Raises an event on the <see cref="EventSynchronizationContext"/> if set, otherwise on the current thread.
    /// </summary>
    /// <param name="handler">The event handler to invoke.</param>
    private void RaiseEvent(EventHandler? handler)
    {
        if (handler is null) return;

        if (EventSynchronizationContext is not null)
        {
            EventSynchronizationContext.Post(_ => handler(this, EventArgs.Empty), null);
        }
        else
        {
            handler(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raises an event with arguments on the <see cref="EventSynchronizationContext"/> if set, otherwise on the current thread.
    /// </summary>
    /// <typeparam name="T">The type of the event arguments.</typeparam>
    /// <param name="handler">The event handler to invoke.</param>
    /// <param name="args">The event arguments.</param>
    private void RaiseEvent<T>(EventHandler<T>? handler, T args)
    {
        if (handler is null) return;

        if (EventSynchronizationContext is not null)
        {
            EventSynchronizationContext.Post(_ => handler(this, args), null);
        }
        else
        {
            handler(this, args);
        }
    }

    #endregion
}
