using System.Text.Json;
using System.Text.Json.Serialization;

namespace StageKit;

/// <summary>
/// Base class for singleton settings objects persisted as JSON files.
/// </summary>
/// <typeparam name="T">The concrete settings file type.</typeparam>
public abstract class RootSettingsFile<T> : SubSettings, IDisposable where T : RootSettingsFile<T>, new()
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

    private Timer? _saveTimer;
#if NET10_0_OR_GREATER
    private readonly Lock _saveLock = new();
#else
    private readonly object _saveLock = new();
#endif
    private bool _dirty;
    private bool _disposed;

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
    public abstract string FileName { get; }

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
        instance.OnLoaded(loadFromFile);
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
    /// Saves the current settings to the configured file location using JSON serialization.
    /// </summary>
    /// <remarks>If the target directory does not exist, it is created automatically. Any exceptions
    /// encountered during the save operation are handled internally and do not propagate to the caller.</remarks>
    public void Save()
    {
        lock (_saveLock)
        {
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
        var saveImmediately = false;

        if (debounceMilliseconds <= 0)
        {
            if (DefaultDebounceSaveMilliseconds <= 0)
            {
                saveImmediately = true;
            }
            else
            {
                debounceMilliseconds = DefaultDebounceSaveMilliseconds;
            }
        }

        lock (_saveLock)
        {
            if (!CanSave)
            {
                _dirty = true;
                return;
            }

            if (saveImmediately)
            {
                _dirty = true;
                debounceMilliseconds = 0;
            }

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
    /// Executes custom logic after the object is saved to persistent storage. Override this method to perform
    /// validation, transformation, or other pre-save operations.
    /// </summary>
    /// <remarks>This method is called automatically during the save process. Derived classes can override it
    /// to implement application-specific behavior after to saving. The base implementation does not perform any
    /// actions.</remarks>
    protected virtual void AfterSave()
    {

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
        if (_disposed) return;
        if (disposing)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
        _disposed = true;
    }
}
