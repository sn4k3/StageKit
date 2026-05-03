using CommunityToolkit.Mvvm.ComponentModel;

namespace StageKit.Demo;

public partial class GeneralAppSettings : SubSettings
{
    [ObservableProperty]
    public partial int MaxThreads { get; set; } = -1;
}

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

    [ObservableProperty]
    public partial GeneralAppSettings General { get; set; } = new();

    public override SubSettings[] SubSettingsCollection => [General];

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