using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace StageKit.Demo;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettings.Instance;
            var recentDocuments = RecentDocuments.Instance;
            var onboarding = OnboardingStateFile.Instance;

            CrashReportsFile.IsEnabled = settings.EnableCrashReporting;
            onboarding.RecordLaunch();
            UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(settings);
            UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(recentDocuments);

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
