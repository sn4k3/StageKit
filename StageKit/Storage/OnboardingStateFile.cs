using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StageKit;

/// <summary>
/// Persists first-run and onboarding completion state.
/// </summary>
public sealed partial class OnboardingStateFile : RootSettingsFile<OnboardingStateFile>
{
    /// <summary>
    /// Gets or sets the onboarding state file name.
    /// </summary>
    public static string OnboardingStateDirectoryPath { get; set; } = ApplicationKit.ConfigsPath;

    /// <summary>
    /// Gets or sets the onboarding state file name.
    /// </summary>
    public static string OnboardingStateFileName { get; set; } = "OnboardingState.json";

    /// <summary>
    /// Gets or sets the UTC time when this state file was created.
    /// </summary>
    [ObservableProperty]
    public partial DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC time of the last recorded launch.
    /// </summary>
    [ObservableProperty]
    public partial DateTime? LastLaunchUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of recorded launches.
    /// </summary>
    [ObservableProperty]
    public partial int LaunchCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when onboarding was completed.
    /// </summary>
    [ObservableProperty]
    public partial DateTime? OnboardingCompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the application-defined onboarding version that was completed.
    /// </summary>
    [ObservableProperty]
    public partial string? OnboardingVersion { get; set; }

    /// <summary>
    /// Gets a value indicating whether onboarding has not been completed.
    /// </summary>
    [JsonIgnore]
    public bool IsFirstRun => OnboardingCompletedUtc is null;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingStateFile"/> class.
    /// </summary>
    public OnboardingStateFile()
    {
        AutoSave = true;
        if (!string.IsNullOrWhiteSpace(OnboardingStateDirectoryPath)) DirectoryPath = OnboardingStateDirectoryPath;
        if (!string.IsNullOrWhiteSpace(OnboardingStateFileName)) FileName = OnboardingStateFileName;
        DefaultDebounceSaveMilliseconds = 100;
    }

    /// <summary>
    /// Records an application launch.
    /// </summary>
    public void RecordLaunch()
    {
        BatchUpdate(() =>
        {
            LaunchCount++;
            LastLaunchUtc = DateTime.UtcNow;
        });
    }

    /// <summary>
    /// Marks onboarding as completed.
    /// </summary>
    /// <param name="version">The application-defined onboarding version.</param>
    public void CompleteOnboarding(string? version = null)
    {
        BatchUpdate(() =>
        {
            OnboardingCompletedUtc = DateTime.UtcNow;
            OnboardingVersion = version;
        });
    }

    /// <summary>
    /// Clears onboarding completion values.
    /// </summary>
    public void ResetOnboarding()
    {
        BatchUpdate(() =>
        {
            OnboardingCompletedUtc = null;
            OnboardingVersion = null;
        });
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"{nameof(CreatedUtc)}: {CreatedUtc}, {nameof(LastLaunchUtc)}: {LastLaunchUtc}, {nameof(LaunchCount)}: {LaunchCount}, {nameof(OnboardingCompletedUtc)}: {OnboardingCompletedUtc}, {nameof(OnboardingVersion)}: {OnboardingVersion}, {nameof(IsFirstRun)}: {IsFirstRun}";
    }
}
