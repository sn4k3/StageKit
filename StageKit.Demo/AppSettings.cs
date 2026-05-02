using CommunityToolkit.Mvvm.ComponentModel;

namespace StageKit.Demo;

public partial class AppSettings : RootSettingsFile<AppSettings>
{
    [ObservableProperty]
    public partial string Theme { get; set; } = "System";

    [ObservableProperty]
    public partial bool EnableCrashReporting { get; set; } = true;

    [ObservableProperty]
    public partial long LastRunTimestamp { get; set; }

    public AppSettings()
    {
        AutoSave = true;
    }

    public override string ToString()
    {
        return
            $"{nameof(Theme)}: {Theme}, {nameof(EnableCrashReporting)}: {EnableCrashReporting}, {nameof(LastRunTimestamp)}: {LastRunTimestamp}";
    }
}