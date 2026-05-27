using Serilog;
using Serilog.Extensions.Logging;
using StageKit;
using StageKit.Demo;
using System.Diagnostics;

Console.WriteLine("Hello from StageKit Demo");

///////////////////
// Configuration //
///////////////////
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

var loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);

ApplicationKit.ApplicationArgs = args;
ApplicationKit.Logger = loggerFactory.CreateLogger("StageKitDemo");
ApplicationKit.UiFrameworkInfo = "Console";
ApplicationKit.ProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "StageKitDemo");
ApplicationKit.Birth = DateTime.UtcNow;

UnhandledExceptions.RegisterAppDomainUnhandledException();
UnhandledExceptions.RegisterTaskSchedulerUnobservedTaskException();
// Example of custom event handler for unhandled exceptions in a UI framework like WPF or Avalonia:
// Dispatcher.UIThread.UnhandledException += (sender, e) => UnhandledExceptions.HandleUnhandledException(e.Exception, "Dispatcher");
UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(AppSettings.Instance);
UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(RecentDocuments.Instance);

CrashReportsFile.IsEnabled = AppSettings.Instance.EnableCrashReporting;

var onboardingState = OnboardingStateFile.Instance;
Console.WriteLine(onboardingState);
onboardingState.RecordLaunch();
onboardingState.CompleteOnboarding();
///////////////////////
// End configuration //
///////////////////////


///////////////////////
//    Crash Report   //
///////////////////////
if (ApplicationKit.HasCrashReportFlag && ApplicationKit.CrashReportIndex > 0)
{
    Console.WriteLine($"Crash report: {ApplicationKit.CrashReportIndex}");
    Console.WriteLine(ApplicationKit.CrashReport);
    Console.WriteLine("Press any key to exit...");
    Console.ReadLine();
    return;
}


using var appGuard = ApplicationInstanceGuard.AcquireGlobal();
if (appGuard.IsSecondary)
{
    Console.WriteLine($"The app {appGuard.PrimaryProcess?.ProcessName} is already running on another window. PID: {appGuard.PrimaryProcess?.Id}");
    Console.WriteLine("Exiting now.");
    return;
}

///////////////////////
//     Main Logic    //
///////////////////////
CrashReportsFile.IsEnabled = AppSettings.Instance.EnableCrashReporting;
AppSettings.Instance.Theme = Random.Shared.Next(0, 3) switch
{
    0 => "System",
    1 => "Light",
    2 => "Dark",
    _ => AppSettings.Instance.Theme
};
AppSettings.Instance.LastRunTimestamp = Stopwatch.GetTimestamp();

while (!AppSettings.Instance.FileExists)
{
    Console.WriteLine($"{AppSettings.Instance.FileName} does not exists, waiting 300ms...");
    await Task.Delay(300);
}
Console.WriteLine(AppSettings.Instance.ToString());

RecentDocuments.Instance.Clear();
RecentDocuments.Instance.Add("document1.docx");
RecentDocuments.Instance.Add("document2.docx");
RecentDocuments.Instance.Add("document3.docx");
RecentDocuments.Instance.Add("document4.docx");
RecentDocuments.Instance.Add("document5.docx");
RecentDocuments.Instance.Add("document6.docx");
RecentDocuments.Instance.Add("document7.docx");

Console.WriteLine("Choose an option:");
Console.WriteLine("1. Throw divide by zero exception");
Console.WriteLine("2. Throw overflow exception");
Console.WriteLine("3. Test SubSetting change with save");
Console.WriteLine("b. Backup configs");
Console.WriteLine("e. Export logs");
Console.WriteLine("q/quit/exit = Quit application");


while (true)
{
    Console.Write("Option: ");
    var line = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(line)) continue;

    if (line.Equals("1", StringComparison.OrdinalIgnoreCase))
    {
        int zero = 0;
        int overflow = int.MaxValue;
        overflow *= 2;
        var cantDivideByZero = overflow / zero;
    }
    else if (line.Equals("2", StringComparison.OrdinalIgnoreCase))
    {
        int overflow = int.MaxValue;
        checked
        {
            overflow *= 2;
        }
    }
    else if (line.Equals("3", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"{nameof(AppSettings.Instance.HasUnsavedChanges)}: {AppSettings.Instance.HasUnsavedChanges}");
        AppSettings.Instance.General.MaxThreads = Random.Shared.Next(1, 101);
        Console.WriteLine($"{nameof(AppSettings.Instance.HasUnsavedChanges)}: {AppSettings.Instance.HasUnsavedChanges}");
        if (AppSettings.Instance.HasUnsavedChanges)
        {
            Console.WriteLine($"Waiting for save: {AppSettings.Instance.CanSave}");
            await AppSettings.Instance.WaitForDebouncedSaveAsync(TimeSpan.FromSeconds(5));
            Console.WriteLine($"Saved, {nameof(AppSettings.Instance.HasUnsavedChanges)}: {AppSettings.Instance.HasUnsavedChanges}");
        }
    }
    else if (line.Equals("b", StringComparison.OrdinalIgnoreCase))
    {
        var backup = ApplicationBackup.Create();
        Console.WriteLine($"Backup created: {backup}");
    }
    else if (line.Equals("e", StringComparison.OrdinalIgnoreCase))
    {
        var backup = SupportBundleExporter.Export();
        Console.WriteLine($"Export created: {backup}");
    }
    else if (line.Equals("exit", StringComparison.OrdinalIgnoreCase)
             || line.Equals("quit", StringComparison.OrdinalIgnoreCase)
             || line.Equals("q", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
}


Console.WriteLine("Awaiting debounced save...");
await RecentDocuments.Instance.WaitForDebouncedSaveAsync(TimeSpan.FromSeconds(5));
