using CommunityToolkit.Mvvm.ComponentModel;

namespace StageKit.Demo;

public partial class AppSettings : RootSettingsFile<AppSettings>
{
    [ObservableProperty]
    public partial string Theme { get; set; } = "System";

    [ObservableProperty]
    public partial string ThemeColor { get; set; } = "Blue";

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
            $"{nameof(Theme)}: {Theme}, {nameof(ThemeColor)}: {ThemeColor}, {nameof(EnableCrashReporting)}: {EnableCrashReporting}, {nameof(LastRunTimestamp)}: {LastRunTimestamp}";
    }
}