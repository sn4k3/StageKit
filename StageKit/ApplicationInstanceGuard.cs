namespace StageKit;

/// <summary>
/// Provides a named single-instance application guard.
/// </summary>
public sealed class ApplicationInstanceGuard : IDisposable
{
    /// <summary>
    /// Gets a suggested global instance name based on the application domain friendly name.
    /// </summary>
    public static string SuggestedInstanceNameGlobal => $"StageKit_{AppDomain.CurrentDomain.FriendlyName}";

    /// <summary>
    /// Gets a suggested instance name based on the application domain friendly name and current user identity.
    /// </summary>
    public static string SuggestedInstanceNamePerUser => $"{SuggestedInstanceNameGlobal}_{Environment.UserDomainName}_{Environment.UserName}";

    private readonly Mutex _mutex;
    private bool _isDisposed;

    /// <summary>
    /// Gets the instance name used by the guard.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets a value indicating whether this process owns the primary instance guard.
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Gets a value indicating whether another process already owns the primary instance guard.
    /// </summary>
    public bool IsSecondary => !IsPrimary;

    private ApplicationInstanceGuard(string instanceName, Mutex mutex, bool isPrimary)
    {
        InstanceName = instanceName;
        _mutex = mutex;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// Acquires a global application instance guard to ensure that only one instance of the application is running
    /// system-wide.
    /// </summary>
    /// <remarks>Use this method to prevent multiple instances of the application from running simultaneously
    /// across the entire system. If a global instance is already active, acquiring the guard may fail depending on the
    /// implementation of <see cref="Acquire"/>.</remarks>
    /// <returns>An <see cref="ApplicationInstanceGuard"/> representing the acquired global instance guard. The caller is
    /// responsible for disposing the guard when it is no longer needed.</returns>
    public static ApplicationInstanceGuard AcquireGlobal()
    {
        return Acquire(SuggestedInstanceNameGlobal);
    }

    /// <summary>
    /// Acquires an application instance guard that is unique to the current user.
    /// </summary>
    /// <remarks>Use this method to ensure that only one instance of the application runs per user. This is
    /// useful in multi-user environments where each user should have at most one active instance.</remarks>
    /// <returns>An ApplicationInstanceGuard that enforces single-instance behavior for the current user context.</returns>
    public static ApplicationInstanceGuard AcquirePerUser()
    {
        return Acquire(SuggestedInstanceNamePerUser);
    }

    /// <summary>
    /// Acquires a named application instance guard.
    /// </summary>
    /// <param name="instanceName">The application instance name.</param>
    /// <returns>An application instance guard that identifies whether the caller is primary or secondary.</returns>
    /// <exception cref="ArgumentException"><paramref name="instanceName"/> is null, empty, or whitespace.</exception>
    public static ApplicationInstanceGuard Acquire(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var mutex = new Mutex(false, instanceName);
        try
        {
            var isPrimary = false;
            try
            {
                isPrimary = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                isPrimary = true;
            }

            return new ApplicationInstanceGuard(instanceName, mutex, isPrimary);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
