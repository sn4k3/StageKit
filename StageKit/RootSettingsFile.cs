using System.Text.Json;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using StageKit.Interfaces;

namespace StageKit;

/// <summary>
/// Base class for singleton settings objects persisted as JSON files.
/// </summary>
/// <typeparam name="T">The concrete settings file type.</typeparam>
public abstract partial class RootSettingsFile<T> : SubSettings, ISavable, IDisposable where T : RootSettingsFile<T>, new()
{
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
    protected virtual JsonSerializerOptions JsonOptions => ApplicationKit.JsonSerializerOptions;

    /// <summary>
    /// Gets the file system path to the application's configuration directory.
    /// </summary>
    [JsonIgnore]
    public virtual string DirectoryPath => ApplicationKit.ConfigPath;

    /// <summary>
    /// Gets the file name of this settings file.
    /// </summary>
    [JsonIgnore]
    public virtual string FileName => $"{GetType().Name}.json";

    /// <summary>
    /// Gets the full file system path to the configuration file.
    /// </summary>
    [JsonIgnore]
    public string FilePath => Path.Combine(DirectoryPath, FileName);

    /// <summary>
    /// Gets a value indicating whether the current state allows the object to be saved.
    /// </summary>
    [JsonIgnore]
    public bool CanSave { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether a debounced save operation is currently pending. This flag is set to <see langword="true"/> when a debounced save is scheduled but has not yet executed, and is reset to <see langword="false"/> once the save operation completes or is canceled.
    /// </summary>
    [JsonIgnore]
    public bool IsDebounceSavePending => _isDebounceSavePending;

    /// <summary>
    /// Gets or sets a value indicating whether property changes automatically schedule a save.
    /// </summary>
    /// <remarks>
    /// Auto-save uses <see cref="DebouncedSave(int)"/> and therefore respects
    /// <see cref="DefaultDebounceSaveMilliseconds"/>.
    /// </remarks>
    [JsonIgnore]
    [ObservableProperty]
    public partial bool AutoSave { get; set; }

    /// <summary>
    /// Gets the default debounce interval, in milliseconds, used to delay save operations.
    /// </summary>
    /// <remarks>This value determines the minimum time to wait before triggering a save after changes occur.
    /// Adjusting the debounce interval can help optimize performance by reducing the frequency of save operations in
    /// scenarios with rapid consecutive changes.<br/>
    /// 0 = Instant save without debounce.</remarks>
    [JsonIgnore]
    protected virtual int DefaultDebounceSaveMilliseconds => 1000;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="RootSettingsFile{T}"/> class and sets up event handlers for property changes based on the initial value of <see cref="AutoSave"/>.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (!IsLoaded || e.PropertyName == nameof(AutoSave)) return;
        Debug.WriteLine($"[{GetType().Name}] Property changed: {e.PropertyName}");
        if (AutoSave)
        {
            DebouncedSave();
        }
    }

    /// <summary>
    /// Loads settings from the configured file location. If the file doesn't exist, returns a new instance.
    /// </summary>
    private static T LoadOrCreate()
    {
        var instance = new T();
        var filePath = instance.FilePath;
        var loadFromFile = false;

        try
        {
            if (File.Exists(filePath))
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.SequentialScan);

                var loaded = JsonSerializer.Deserialize<T>(stream, instance.JsonOptions);

                if (loaded is not null)
                {
                    instance = loaded;
                    loadFromFile = true;
                }
            }
        }
        catch (Exception e)
        {
            UnhandledExceptions.HandleSafeException(e, $"LoadOrCreate {instance.FileName}");
        }

        instance.CanSave = true;
        instance.OnLoadedCore(loadFromFile);
        return instance;
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
        lock (_saveLock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isDebounceSavePending = false;
            if (!CanSave) return;
            CanSave = false;
            _dirty = false;
            try
            {
                Directory.CreateDirectory(DirectoryPath);

                BeforeSave();
                using var stream = new FileStream(
                    FilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.None);

                JsonSerializer.Serialize(stream, this, GetType(), JsonOptions);
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
            }
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
                Debug.WriteLine($"[{GetType().Name}] DebouncedSave({debounceMilliseconds})");

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

        Debug.WriteLine($"[{GetType().Name}] DebouncedSave({debounceMilliseconds})");

        lock (_saveLock)
        {
            if (!CanSave)
            {
                _dirty = true;
                return;
            }

            _isDebounceSavePending = true;
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
    /// Cancels any pending debounced save operation.
    /// </summary>
    public void CancelDebouncedSave()
    {
        lock (_saveLock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isDebounceSavePending = false;
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

        using var timeoutTokenSource = timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        using var linkedTokenSource = timeoutTokenSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);

        try
        {
            while (IsDebounceSavePending)
            {
                await Task.Delay(100, linkedTokenSource.Token).ConfigureAwait(false);
            }

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

    /// <summary>
    /// Checks if the settings file exists.
    /// </summary>
    [JsonIgnore]
    public bool FileExists => File.Exists(FilePath);

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
        }
        IsDisposed = true;
    }
}
