using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using StageKit.Interfaces;

namespace StageKit;

/// <summary>
/// Base class for singleton settings objects persisted as JSON files.
/// </summary>
/// <typeparam name="T">The concrete settings file type.</typeparam>
public abstract class RootSettingsFile<T> : SubSettings, ISavable, IDisposable where T : RootSettingsFile<T>, new()
{
    #region Static members
    /// <summary>
    /// The lazy-loaded singleton instance of the settings class.
    /// </summary>
    private static readonly Lazy<T> InstanceLazy = new(LoadOrCreate);

    /// <summary>
    /// The global singleton instance of the settings class.
    /// </summary>
    public static T Instance => InstanceLazy.Value;

    /// <summary>
    /// Gets a value indicating whether the singleton instance has been created.
    /// </summary>
    public static bool IsInstanceCreated => InstanceLazy.IsValueCreated;
    #endregion

    #region Members

    /// <summary>
    /// A timer used to schedule debounced save operations. When changes occur that trigger a save, the timer is started or reset to delay the save operation until a specified debounce interval has elapsed. This helps to optimize performance by reducing the frequency of save operations during periods of rapid consecutive changes.
    /// </summary>
    private Timer? _saveTimer;

    /// <summary>
    /// An object used to synchronize access to save operations, ensuring that only one save can occur at a time and preventing race conditions when multiple threads attempt to save concurrently.
    /// </summary>
#if NET10_0_OR_GREATER
    private readonly Lock _saveLock = new();
#else
    private readonly object _saveLock = new();
#endif

    /// <summary>
    /// Indicates whether there are unsaved changes that occurred while a save operation was in progress. If <see cref="CanSave"/> is <see langword="false"/>, changes will set this flag to ensure that a save is attempted after the current save operation completes.
    /// </summary>
    private bool _dirty;

    /// <summary>
    /// Indicates whether a debounced save operation has been scheduled but has not completed yet.
    /// </summary>
    private volatile bool _isDebounceSavePending;

    /// <summary>
    /// Signals completion of an in-flight or pending save to async waiters. Replaces polling.
    /// </summary>
    private TaskCompletionSource<bool>? _saveCompletion;

    /// <summary>
    /// Tracks child settings objects that are already wired into root auto-save.
    /// </summary>
    private readonly HashSet<SubSettings> _trackedSubSettings = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Tracks nested auto-save suspension scopes.
    /// </summary>
    private int _autoSaveSuspensionDepth;

    /// <summary>
    /// Indicates whether settings changed while auto-save was suspended.
    /// </summary>
    private bool _autoSaveSuspensionChanged;

    /// <summary>
    /// Indicates whether suspended changes should be saved when the outermost suspension scope is disposed.
    /// </summary>
    private bool _autoSaveSuspensionSaveOnDispose = true;

    /// <summary>
    /// Indicates whether the object has been disposed. Once disposed, the object should not be used and any further operations may throw exceptions or have undefined behavior.
    /// </summary>
    protected bool IsDisposed;

    #endregion

    #region Properties
    /// <summary>
    /// Gets the default options used for JSON serialization within this class.
    /// </summary>
    /// <remarks>This property provides access to the application's configured <see
    /// cref="JsonSerializerOptions"/> instance. Use these options to ensure consistent serialization and
    /// deserialization behavior across the application.</remarks>
    [JsonIgnore]
    protected JsonSerializerOptions JsonOptions { get; init; } = ApplicationKit.JsonSerializerOptions;

    /// <summary>
    /// Gets or sets the persisted settings schema version.
    /// </summary>
    public int SettingsVersion { get; set; }

    /// <summary>
    /// Gets the settings schema version expected by the current application.
    /// </summary>
    [JsonIgnore]
    protected virtual int CurrentSettingsVersion => 1;

    /// <summary>
    /// Gets the file system path to the application's configuration directory.
    /// </summary>
    [JsonIgnore]
    public string DirectoryPath { get; init; } = ApplicationKit.ProfilePath;

    /// <summary>
    /// Gets the file name of this settings file.
    /// </summary>
    [JsonIgnore]
    public string FileName { get; init; }

    /// <summary>
    /// Gets the full file system path to the configuration file.
    /// </summary>
    [JsonIgnore]
    public string FilePath => Path.Combine(DirectoryPath, FileName);

    /// <summary>
    /// Checks if the settings file exists.
    /// </summary>
    [JsonIgnore]
    public bool FileExists => File.Exists(FilePath);

    /// <summary>
    /// Backing field for <see cref="CanSave"/>. Volatile to ensure cross-thread visibility for unlocked readers.
    /// </summary>
    [JsonIgnore]
    private volatile bool _canSave;

    /// <summary>
    /// Gets a value indicating whether the current state allows the object to be saved.
    /// </summary>
    [JsonIgnore]
    public bool CanSave
    {
        get => _canSave;
        protected set => _canSave = value;
    }

    /// <summary>
    /// Gets a value indicating whether a debounced save operation is currently pending. This flag is set to <see langword="true"/> when a debounced save is scheduled but has not yet executed, and is reset to <see langword="false"/> once the save operation completes or is canceled.
    /// </summary>
    [JsonIgnore]
    public bool IsDebounceSavePending => _isDebounceSavePending;

    /// <summary>
    /// Gets the number of successful saves completed by this in-memory settings instance.
    /// </summary>
    [JsonIgnore]
    public int SaveCount { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether property changes automatically schedule a save.
    /// </summary>
    /// <remarks>
    /// Auto-save uses <see cref="DebouncedSave(int)"/> and therefore respects
    /// <see cref="DefaultDebounceSaveMilliseconds"/>.
    /// </remarks>
    [JsonIgnore]
    public bool AutoSave { get; set; }

    /// <summary>
    /// Gets the default debounce interval, in milliseconds, used to delay save operations.
    /// </summary>
    /// <remarks>This value determines the minimum time to wait before triggering a save after changes occur.
    /// Adjusting the debounce interval can help optimize performance by reducing the frequency of save operations in
    /// scenarios with rapid consecutive changes.<br/>
    /// 0 = Instant save without debounce.</remarks>
    [JsonIgnore]
    public int DefaultDebounceSaveMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets a collection of nested <see cref="SubSettings"/> objects that are part of this settings file.<br/>
    /// This property can be overridden in derived classes to return any child settings objects that should be included in the save and load process.<br/>
    /// By default, this returns an empty array, but derived classes can return any relevant sub-settings to ensure they are properly persisted alongside the root settings.
    /// This allows for a hierarchical organization of settings within a single file while maintaining clear separation of concerns between different settings categories.
    /// </summary>
    [JsonIgnore]
    public virtual SubSettings[] SubSettingsCollection => [];

    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="RootSettingsFile{T}"/> class.
    /// </summary>
    protected RootSettingsFile()
    {
        var typeName = GetType().Name;
        if (typeName.EndsWith("File", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName[..^"File".Length];
        }
        FileName = $"{typeName}.json";
    }
    #endregion

    #region Events
    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (!IsLoaded) return;
        ScheduleAutoSaveAfterChange();
    }

    private void SubSettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded) return;
        HasUnsavedChanges = true;
        ScheduleAutoSaveAfterChange();
    }
    #endregion

    /// <summary>
    /// Loads settings from the configured file location. If the file doesn't exist, returns a new instance.
    /// </summary>
    private static T LoadOrCreate()
    {
        var instance = new T();
        var filePath = instance.FilePath;
        var loadFromFile = false;
        var hasPostLoadChanges = false;

        if (File.Exists(filePath))
        {
            T? loaded = null;
            try
            {
                using (var stream = new FileStream(
                           filePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           4096,
                           FileOptions.SequentialScan))
                {
                    loaded = JsonSerializer.Deserialize<T>(stream, instance.JsonOptions);
                }

                if (loaded is not null)
                {
                    var currentSettingsVersion = loaded.CurrentSettingsVersion;
                    if (loaded.SettingsVersion > currentSettingsVersion)
                    {
                        loaded.Dispose();
                        loaded = null;
                        RenameInvalidSettingsFile(filePath, instance.FileName, "unsupported-version");
                    }
                    else
                    {
                        instance.Dispose();
                        instance = loaded;
                        loaded = null;
                        loadFromFile = true;

                        if (instance.SettingsVersion < currentSettingsVersion)
                        {
                            var migrationContext = new SettingsMigrationContext(
                                instance.SettingsVersion,
                                currentSettingsVersion);
                            instance.MigrateSettings(migrationContext);
                            instance.SettingsVersion = currentSettingsVersion;
                            hasPostLoadChanges = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnhandledExceptions.HandleSafeException(e, $"LoadOrCreate {instance.FileName}");

                RenameInvalidSettingsFile(filePath, instance.FileName, "corrupt");
            }
            finally
            {
                loaded?.Dispose();
            }
        }

        if (!loadFromFile)
        {
            instance.SettingsVersion = instance.CurrentSettingsVersion;
        }

        var validationContext = new SettingsValidationContext(loadFromFile);
        instance.ValidateSettings(validationContext);
        hasPostLoadChanges |= validationContext.HasChanges;

        instance.CanSave = true;
        instance.OnLoadedCore(loadFromFile);

        foreach (var subSettings in instance.SubSettingsCollection)
        {
            subSettings.OnLoadedCore(loadFromFile);
        }

        if (hasPostLoadChanges)
        {
            instance.HasUnsavedChanges = true;
        }

        instance.TrackSubSettings();
        if (hasPostLoadChanges)
        {
            instance.ScheduleAutoSaveAfterChange();
        }

        return instance;
    }

    private static void RenameInvalidSettingsFile(string filePath, string fileName, string reason)
    {
        try
        {
            var backupPath = $"{filePath}.{reason}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(filePath, backupPath);
        }
        catch (Exception moveEx)
        {
            UnhandledExceptions.HandleSafeException(moveEx, $"Backup {reason} {fileName}");
        }
    }

    /// <summary>
    /// Migrates loaded settings from an older schema version to <see cref="CurrentSettingsVersion"/>.
    /// </summary>
    /// <param name="context">The migration context.</param>
    protected virtual void MigrateSettings(SettingsMigrationContext context)
    {
    }

    /// <summary>
    /// Validates and optionally repairs settings before the settings object is marked as loaded.
    /// </summary>
    /// <param name="context">The validation context.</param>
    protected virtual void ValidateSettings(SettingsValidationContext context)
    {
    }

    /// <summary>
    /// Executes custom logic before the object is saved to persistent storage. Override this method to perform
    /// validation, transformation, or other pre-save operations.
    /// </summary>
    /// <remarks>This method is called automatically during the save process. Derived classes can override it
    /// to implement application-specific behavior prior to saving. The base implementation does not perform any
    /// actions.</remarks>
    protected virtual void BeforeSave()
    {

    }

    /// <summary>
    /// Executes custom logic after the object has been saved to persistent storage. Override this method to perform
    /// post-save operations such as logging, notification, or cleanup.
    /// </summary>
    /// <remarks>This method is called automatically during the save process. Derived classes can override it
    /// to implement application-specific behavior after saving. The base implementation does not perform any
    /// actions.</remarks>
    protected virtual void AfterSave()
    {

    }

    /// <summary>
    /// Saves the current settings to the configured file location using JSON serialization.
    /// </summary>
    /// <remarks>If the target directory does not exist, it is created automatically. Any exceptions
    /// encountered during the save operation are handled internally and do not propagate to the caller.</remarks>
    public void Save()
    {
        TaskCompletionSource<bool>? completionToSignal = null;
        try
        {
            lock (_saveLock)
            {
                _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                if (!CanSave || (!HasUnsavedChanges && FileExists))
                {
                    _isDebounceSavePending = false;
                    completionToSignal = _saveCompletion;
                    _saveCompletion = null;
                    return;
                }
                CanSave = false;
                _dirty = false;

                try
                {
                    BeforeSave();

                    SafeFile.Write(FilePath, stream =>
                    {
                        JsonSerializer.Serialize(stream, this, GetType(), JsonOptions);
                    });

                    SaveCount++;
                    ClearUnsavedChangesCore();
                    AfterSave();
                }
                catch (Exception e)
                {
                    UnhandledExceptions.HandleSafeException(e, $"Save {FileName}");
                }

                CanSave = true;

                if (_dirty)
                {
                    _dirty = false;
                    var delay = DefaultDebounceSaveMilliseconds <= 0 ? 0 : DefaultDebounceSaveMilliseconds;
                    _isDebounceSavePending = true;
                    if (_saveTimer is null)
                    {
                        _saveTimer = new Timer(_ => Save(), null, delay, Timeout.Infinite);
                    }
                    else
                    {
                        _saveTimer.Change(delay, Timeout.Infinite);
                    }
                    return;
                }

                _isDebounceSavePending = false;
                completionToSignal = _saveCompletion;
                _saveCompletion = null;
            }
        }
        finally
        {
            completionToSignal?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Saves the settings after a debounce delay. Multiple rapid calls will reset the timer.
    /// </summary>
    /// <param name="debounceMilliseconds">The delay in milliseconds before saving. Default is <see cref="DefaultDebounceSaveMilliseconds"/>.</param>
    public void DebouncedSave(int debounceMilliseconds = 0)
    {
        if (debounceMilliseconds <= 0)
        {
            if (DefaultDebounceSaveMilliseconds <= 0)
            {
                lock (_saveLock)
                {
                    if (!CanSave)
                    {
                        _dirty = true;
                        return;
                    }
                }

                Save();
                return;
            }

            debounceMilliseconds = DefaultDebounceSaveMilliseconds;
        }

        lock (_saveLock)
        {
            if (!CanSave)
            {
                _dirty = true;
                return;
            }

            _isDebounceSavePending = true;
            _saveCompletion ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_saveTimer is null)
            {
                _saveTimer = new Timer(_ => Save(), null, debounceMilliseconds, Timeout.Infinite);
            }
            else
            {
                _saveTimer.Change(debounceMilliseconds, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// Temporarily suspends automatic save scheduling.
    /// </summary>
    /// <param name="saveOnDispose">Whether to schedule one save when the outermost suspension scope is disposed and settings changed.</param>
    /// <returns>A disposable scope that resumes auto-save scheduling when disposed.</returns>
    public IDisposable SuspendAutoSave(bool saveOnDispose = true)
    {
        lock (_saveLock)
        {
            if (_autoSaveSuspensionDepth == 0)
            {
                _autoSaveSuspensionChanged = false;
                _autoSaveSuspensionSaveOnDispose = true;
            }

            _autoSaveSuspensionDepth++;
            _autoSaveSuspensionSaveOnDispose &= saveOnDispose;
        }

        return new AutoSaveSuspensionScope(this);
    }

    /// <summary>
    /// Runs a group of settings changes while automatic save scheduling is suspended.
    /// </summary>
    /// <param name="update">The update action to run.</param>
    /// <param name="saveOnComplete">Whether to schedule one save after the update completes and settings changed.</param>
    public void BatchUpdate(Action update, bool saveOnComplete = true)
    {
        ArgumentNullException.ThrowIfNull(update);

        using var scope = SuspendAutoSave(saveOnComplete);
        update();
    }

    /// <summary>
    /// Schedules auto-save for a detected settings change, respecting suspended auto-save scopes.
    /// </summary>
    /// <param name="debounceMilliseconds">The debounce interval to use, or 0 for the default.</param>
    protected void ScheduleAutoSaveAfterChange(int debounceMilliseconds = 0)
    {
        lock (_saveLock)
        {
            if (_autoSaveSuspensionDepth > 0)
            {
                _autoSaveSuspensionChanged = true;
                return;
            }
        }

        if (!AutoSave) return;

        DebouncedSave(debounceMilliseconds);
    }

    private void ResumeAutoSaveSuspension()
    {
        var shouldSave = false;
        lock (_saveLock)
        {
            if (_autoSaveSuspensionDepth <= 0)
            {
                return;
            }

            _autoSaveSuspensionDepth--;
            if (_autoSaveSuspensionDepth == 0)
            {
                shouldSave = AutoSave && _autoSaveSuspensionChanged && _autoSaveSuspensionSaveOnDispose;
                _autoSaveSuspensionChanged = false;
                _autoSaveSuspensionSaveOnDispose = true;
            }
        }

        if (shouldSave)
        {
            DebouncedSave();
        }
    }

    private sealed class AutoSaveSuspensionScope : IDisposable
    {
        private RootSettingsFile<T>? _owner;

        public AutoSaveSuspensionScope(RootSettingsFile<T> owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.ResumeAutoSaveSuspension();
        }
    }

    /// <summary>
    /// Cancels any pending debounced save operation.
    /// </summary>
    public void CancelDebouncedSave()
    {
        TaskCompletionSource<bool>? completionToSignal;
        lock (_saveLock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isDebounceSavePending = false;
            completionToSignal = _saveCompletion;
            _saveCompletion = null;
        }
        completionToSignal?.TrySetResult(false);
    }

    /// <summary>
    /// Clears dirty state for this root settings object and any tracked sub-settings.
    /// </summary>
    protected internal override void ClearUnsavedChangesCore()
    {
        base.ClearUnsavedChangesCore();
        foreach (var subSettings in SubSettingsCollection)
        {
            subSettings.ClearUnsavedChangesCore();
        }
    }

    /// <summary>
    /// Subscribes to <see cref="INotifyPropertyChanged.PropertyChanged"/> events for any <see cref="SubSettings"/> objects in the <see cref="SubSettingsCollection"/> to track changes and trigger auto-save when they change. This method is called during initialization to ensure that changes in child settings are properly detected and handled by the root settings file's auto-save mechanism.
    /// </summary>
    private void TrackSubSettings()
    {
        foreach (var subSettings in SubSettingsCollection)
        {
            if (_trackedSubSettings.Add(subSettings))
            {
                subSettings.PropertyChanged += SubSettingsOnPropertyChanged;
            }
        }
    }

    /// <summary>
    /// Waits until the current debounced save operation completes or the timeout elapses.
    /// </summary>
    /// <param name="timeout">The maximum time to wait, or <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
    /// <param name="cancellationToken">A token that can cancel the wait.</param>
    /// <returns><see langword="true"/> when no debounced save is pending; otherwise, <see langword="false"/> when the timeout elapses.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeout"/> is negative and is not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public async Task<bool> WaitForDebouncedSaveAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be non-negative or infinite.");
        }

        Task completionTask;
        lock (_saveLock)
        {
            if (!_isDebounceSavePending && _saveCompletion is null)
            {
                return true;
            }

            _saveCompletion ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionTask = _saveCompletion.Task;
        }

        using var timeoutTokenSource = timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        using var linkedTokenSource = timeoutTokenSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);

        try
        {
            await completionTask.WaitAsync(linkedTokenSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (
            timeoutTokenSource?.IsCancellationRequested == true &&
            !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Saves the current settings to the configured file location using JSON serialization.
    /// </summary>
    /// <remarks>If the target directory does not exist, it is created automatically. Any exceptions
    /// encountered during the save operation are handled internally and do not propagate to the caller.</remarks>
    public static void SaveInstance()
    {
        if (!IsInstanceCreated) return;
        Instance.Save();
    }

    /// <summary>
    /// Deletes the settings file if it exists.
    /// </summary>
    public void DeleteFile()
    {
        try
        {
            if (FileExists)
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception e)
        {
            UnhandledExceptions.HandleSafeException(e, $"Delete {FileName}");
        }
    }

    #region Dispose
    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged resources and optionally releases managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release managed resources; otherwise, <see langword="false"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (disposing)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            _saveCompletion?.TrySetResult(false);
            _saveCompletion = null;
            foreach (var subSettings in _trackedSubSettings)
            {
                subSettings.PropertyChanged -= SubSettingsOnPropertyChanged;
            }

            _trackedSubSettings.Clear();
        }
        IsDisposed = true;
    }
    #endregion
}
