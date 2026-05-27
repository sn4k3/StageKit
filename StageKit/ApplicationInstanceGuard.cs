using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using StageKit.Primitives;

namespace StageKit;

/// <summary>
/// Provides a named single-instance application guard.
/// </summary>
public sealed class ApplicationInstanceGuard : DisposableObject
{
    #region Members
    /// <summary>
    /// Gets a suggested global instance name based on the application domain friendly name.
    /// </summary>
    public static string SuggestedInstanceNameGlobal => $"StageKit_{AppDomain.CurrentDomain.FriendlyName}";

    /// <summary>
    /// Gets a suggested instance name based on the application domain friendly name and current user identity.
    /// </summary>
    public static string SuggestedInstanceNamePerUser => $"{SuggestedInstanceNameGlobal}_{Environment.UserDomainName}_{Environment.UserName}";

    /// <summary>
    /// Holds the primary process associated with the current application instance.
    /// </summary>
    private Process? _primaryProcess;

    /// <summary>
    /// Holds the mutex used to enforce single-instance behavior. The mutex is named based on the instance name provided
    /// </summary>
    private readonly Mutex _mutex;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the instance name used by the guard.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets a value indicating whether this process owns the primary instance guard.
    /// </summary>
    [MemberNotNullWhen(true, nameof(PrimaryProcess))]
    public bool IsPrimary { get; }

    /// <summary>
    /// Gets a value indicating whether another process already owns the primary instance guard.
    /// </summary>
    [MemberNotNullWhen(false, nameof(PrimaryProcess))]
    public bool IsSecondary => !IsPrimary;

    /// <summary>
    /// Gets the primary process associated with the current application instance.
    /// </summary>
    /// <remarks>If the current process is designated as primary, this property returns the current process.
    /// Otherwise, it returns the first process with the same name as the current process, ordered by process ID,
    /// excluding the current process itself.</remarks>
    public Process? PrimaryProcess
    {
        get
        {
            if (_primaryProcess is not null) return _primaryProcess;

            if (IsPrimary)
            {
                _primaryProcess = Process.GetCurrentProcess();
                return _primaryProcess;
            }

            using var currentProcess = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName).OrderBy(p => p.Id))
            {
                if (process.Id == Environment.ProcessId)
                {
                    process.Dispose();
                    continue;
                }

                if (_primaryProcess is null)
                {
                    _primaryProcess = process;
                    continue;
                }

                process.Dispose();
            }

            return _primaryProcess;
        }
    }
    #endregion

    #region Constructor
    private ApplicationInstanceGuard(string instanceName, Mutex mutex, bool isPrimary)
    {
        InstanceName = instanceName;
        _mutex = mutex;
        IsPrimary = isPrimary;
    }
    #endregion

    #region Static Methods

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
            bool isPrimary;
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
    #endregion

    #region Dispose
    /// <inheritdoc />
    protected override void DisposeManaged()
    {
        try
        {
            if (IsPrimary)
            {
                _mutex.ReleaseMutex();
            }
        }
        finally
        {
            _primaryProcess?.Dispose();
            _primaryProcess = null;
            _mutex.Dispose();
        }
    }

    #endregion
}
