using Avalonia;
using Avalonia.Threading;
using Serilog;
using Serilog.Extensions.Logging;
using StageKit;

namespace StageKit.Demo;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Debug()
            .CreateLogger();

        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        ApplicationKit.ApplicationName = "StageKit.Demo";
        ApplicationKit.ApplicationArgs = args;
        ApplicationKit.Logger = loggerFactory.CreateLogger("StageKit.Demo");
        ApplicationKit.UiFrameworkInfo = "Avalonia 12";
        ApplicationKit.ParseProfilePathFromArgs();
        ApplicationKit.Birth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        UnhandledExceptions.IgnoreAvaloniaSafeExceptions();
        UnhandledExceptions.RegisterAppDomainUnhandledException();
        UnhandledExceptions.RegisterTaskSchedulerUnobservedTaskException();
        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            UnhandledExceptions.HandleUnhandledException(
                eventArgs.Exception,
                "[AvaloniaDispatcherUnhandledException]");
        };

        ApplicationInstanceGuard? instanceGuard = null;
        if (!ApplicationKit.HasCrashReportFlag)
        {
            instanceGuard = ApplicationInstanceGuard.AcquirePerUser();
            if (instanceGuard.IsSecondary)
            {
                Log.Information("StageKit.Demo is already running in process {ProcessId}",
                    instanceGuard.PrimaryProcess?.Id);
                instanceGuard.Dispose();
                return 0;
            }
        }

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            instanceGuard?.Dispose();
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
