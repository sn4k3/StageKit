using Microsoft.Extensions.Logging;
using System.Diagnostics;
using StageKit.Interfaces;
using StageKit.Runtime;

namespace StageKit;

/// <summary>
/// Provides helpers for registering process-wide unhandled exception handlers.
/// </summary>
public static class UnhandledExceptions
{
    #region Members

    /// <summary>
    /// Indicates whether <see cref="CurrentDomainOnUnhandledException"/> is registered with <see cref="AppDomain.UnhandledException"/>.
    /// </summary>
    private static bool _isAppDomainUnhandledExceptionRegistered;

    /// <summary>
    /// Indicates whether <see cref="TaskSchedulerOnUnobservedTaskException"/> is registered with <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// </summary>
    private static bool _isTaskSchedulerUnobservedTaskExceptionRegistered;

    /// <summary>
    /// Synchronizes one-time exception handler registration.
    /// </summary>
#if NET10_0_OR_GREATER
    private static readonly Lock RegistrationLock = new();
#else
    private static readonly object RegistrationLock = new();
#endif

    #endregion

    #region Configurations
    /// <summary>
    /// Gets exception types that should be ignored when evaluating unhandled exceptions.
    /// </summary>
    /// <remarks>
    /// Not thread-safe. Configure during application startup before any exception handler runs.
    /// Concurrent mutation while exceptions are being handled is undefined behavior.
    /// </remarks>
    public static HashSet<Type> IgnoredExceptionList { get; } = [];

    /// <summary>
    /// Gets message fragments that should cause exceptions to be ignored.
    /// </summary>
    /// <remarks>
    /// Not thread-safe. Configure during application startup before any exception handler runs.
    /// Concurrent mutation while exceptions are being handled is undefined behavior.
    /// </remarks>
    public static HashSet<string> IgnoredExceptionMessages { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets settings files that should be saved before process termination when a fatal unhandled exception occurs.
    /// </summary>
    public static HashSet<ISavable> SettingsFilesToSaveBeforeCrash { get; } = [];

    /// <summary>
    /// Gets or sets the process exit code used after a fatal unhandled exception.
    /// </summary>
    public static int FatalExitCode { get; set; } = 9;
    #endregion

    #region Events
    /// <summary>
    /// Gets or sets an optional crash report handler.
    /// </summary>
    /// <remarks>
    /// Return <see langword="true"/> to indicate the crash report was handled. Return
    /// <see langword="false"/> to let StageKit launch a crash report instance when possible.
    /// </remarks>
    public static Func<CrashReport, bool>? HandleCrashReport { get; set; }

    /// <summary>
    /// Occurs immediately before <see cref="Environment.Exit(int)"/> is called.
    /// </summary>
    public static event EventHandler? BeforeForcedExit;
    #endregion

    #region Methods
    /// <summary>
    /// Determines whether an exception should be ignored.
    /// </summary>
    /// <param name="ex">The exception to evaluate.</param>
    /// <returns><see langword="true"/> when the exception should be ignored; otherwise, <see langword="false"/>.</returns>
    public static bool CanIgnoreException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        foreach (var exception in TraverseExceptions(ex))
        {
            var exceptionType = exception.GetType();
            foreach (var ignoredType in IgnoredExceptionList)
            {
                if (ignoredType.IsAssignableFrom(exceptionType))
                {
                    return true;
                }
            }

            foreach (var msg in IgnoredExceptionMessages)
            {
                if (exception.Message.Contains(msg, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Adds known benign Avalonia DBus exception message fragments to <see cref="IgnoredExceptionMessages"/>.
    /// </summary>
    public static void IgnoreAvaloniaSafeExceptions()
    {
        IgnoredExceptionMessages.Add("org.freedesktop.DBus.Error.ServiceUnknown");
        IgnoredExceptionMessages.Add("org.freedesktop.DBus.Error.UnknownMethod");
    }

    /// <summary>
    /// Registers <see cref="CurrentDomainOnUnhandledException"/> with <see cref="AppDomain.UnhandledException"/>.
    /// </summary>
    public static void RegisterAppDomainUnhandledException()
    {
        lock (RegistrationLock)
        {
            if (_isAppDomainUnhandledExceptionRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            _isAppDomainUnhandledExceptionRegistered = true;
        }
    }

    /// <summary>
    /// Registers <see cref="TaskSchedulerOnUnobservedTaskException"/> with <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// </summary>
    public static void RegisterTaskSchedulerUnobservedTaskException()
    {
        lock (RegistrationLock)
        {
            if (_isTaskSchedulerUnobservedTaskExceptionRegistered)
            {
                return;
            }

            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            _isTaskSchedulerUnobservedTaskExceptionRegistered = true;
        }
    }

    /// <summary>
    /// Handles unhandled exceptions raised by the current application domain.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The unhandled exception event data.</param>
    public static void CurrentDomainOnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        if (CanIgnoreException(ex))
        {
            ApplicationKit.Logger?.LogWarning(ex, "[CurrentDomainUnhandledException:Ignored]");
            return;
        }

        HandleUnhandledException(ex, "[CurrentDomainUnhandledException]", false);
    }

    /// <summary>
    /// Handles unobserved task exceptions raised by <see cref="TaskScheduler"/>.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The unobserved task exception event data.</param>
    public static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (CanIgnoreException(e.Exception))
        {
            ApplicationKit.Logger?.LogWarning(e.Exception, "[UnobservedTaskException:Ignored]");
            e.SetObserved();
            return;
        }

        HandleUnhandledException(e.Exception, "[UnobservedTaskException]", false);
    }

    /// <summary>
    /// Logs, records, and terminates the process for a fatal unhandled exception.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="category">The log and crash report category.</param>
    /// <param name="searchForIgnoredExceptions">Whether ignore rules should be checked before handling.</param>
    public static void HandleUnhandledException(
        Exception ex,
        string? category = null,
        bool searchForIgnoredExceptions = true)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (searchForIgnoredExceptions && CanIgnoreException(ex))
        {
            try
            {
                ApplicationKit.Logger?.LogWarning(ex, category);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            return;
        }

        try
        {
            ApplicationKit.Logger?.LogCritical(ex, category);

            if (!ApplicationKit.HasCrashReportFlag)
            {
                var report = new CrashReport(ex, category ?? string.Empty);

                if (CrashReportsFile.IsEnabled)
                {
                    CrashReportsFile.Instance.Add(report);
                }


                var spawnProcess = true;
                if (HandleCrashReport is not null)
                {
                    spawnProcess = !HandleCrashReport.Invoke(report);
                }

                if (spawnProcess && !string.IsNullOrWhiteSpace(ApplicationKit.CrashReportFlag) && EntryApplication.IsExecutablePathKnown)
                {
                    EntryApplication.LaunchNewInstance(ApplicationKit.CrashReportFlag, report.Id.ToString());
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        try
        {
            PanicSaveSettingsFiles();
            BeforeForcedExit?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        Environment.Exit(FatalExitCode);
    }

    /// <summary>
    /// Logs an exception that does not require process termination.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="category">The log category.</param>
    /// <param name="logLevel">The log level used for the exception.</param>
    public static void HandleSafeException(
        Exception ex,
        string? category = null,
        LogLevel logLevel = LogLevel.Error)
    {
        ArgumentNullException.ThrowIfNull(ex);

        try
        {
            ApplicationKit.Logger?.Log(logLevel, ex, category);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    /// <summary>
    /// Saves all settings files in <see cref="SettingsFilesToSaveBeforeCrash"/>. Should be called from a process-wide unhandled exception handler before process termination to attempt to preserve user settings.
    /// </summary>
    public static void PanicSaveSettingsFiles()
    {
        foreach (var savable in SettingsFilesToSaveBeforeCrash)
        {
            try
            {
                savable.Save();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }

    /// <summary>
    /// Traverses an exception and its inner exceptions, including all inner exceptions of any encountered <see cref="AggregateException"/> instances.
    /// </summary>
    /// <param name="exception">The exception to traverse.</param>
    /// <returns>A sequence containing the exception and its inner exceptions.</returns>
    public static IEnumerable<Exception> TraverseExceptions(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                yield return innerException;
            }
        }
        else
        {
            var currentException = exception;
            do
            {
                yield return currentException;
                currentException = currentException.InnerException;
            } while (currentException is not null);
        }
    }
    #endregion
}
