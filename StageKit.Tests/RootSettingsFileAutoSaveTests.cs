namespace StageKit.Tests;

public sealed class RootSettingsFileAutoSaveTests
{
    [Fact]
    public void PropertyChanged_WhenAutoSaveEnabled_SavesFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPathOverride = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        settings.Name = "saved";

        Assert.True(File.Exists(settings.FilePath));
        Assert.Contains("saved", File.ReadAllText(settings.FilePath));
        settings.DeleteFile();
    }

    [Fact]
    public void PropertyChanged_WhenAutoSaveDisabled_DoesNotSaveFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPathOverride = directoryPath,
            AutoSave = false
        };
        settings.EnableSaving();

        settings.Name = "not-saved";

        Assert.False(File.Exists(settings.FilePath));
    }

    [Fact]
    public async Task Save_WhenDebouncedSaveIsPending_CancelsPendingSave()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new DelayedAutoSaveSettings
        {
            DirectoryPathOverride = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        settings.Name = "pending";
        settings.Save();

        await Task.Delay(settings.DebounceMilliseconds * 3, TestContext.Current.CancellationToken);

        Assert.Equal(1, settings.SaveCount);
        settings.DeleteFile();
    }

    [Fact]
    public async Task WaitForDebouncedSaveAsync_WhenSaveCompletes_ReturnsTrue()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new DelayedAutoSaveSettings
        {
            DirectoryPathOverride = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        settings.Name = "pending";

        Assert.True(settings.IsDebounceSavePending);

        var completed = await settings.WaitForDebouncedSaveAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(completed);
        Assert.False(settings.IsDebounceSavePending);
        Assert.Equal(1, settings.SaveCount);
        settings.DeleteFile();
    }

    [Fact]
    public async Task WaitForDebouncedSaveAsync_WhenTimeoutElapses_ReturnsFalse()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new DelayedAutoSaveSettings
        {
            DirectoryPathOverride = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        settings.Name = "pending";

        var completed = await settings.WaitForDebouncedSaveAsync(
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken);

        Assert.False(completed);
        Assert.True(settings.IsDebounceSavePending);
        settings.CancelDebouncedSave();
    }

    [Fact]
    public async Task WaitForDebouncedSaveAsync_WhenCanceled_Throws()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new DelayedAutoSaveSettings
        {
            DirectoryPathOverride = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();
        using var cancellationTokenSource = new CancellationTokenSource();

        settings.Name = "pending";
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => settings.WaitForDebouncedSaveAsync(TimeSpan.FromSeconds(5), cancellationTokenSource.Token));

        settings.CancelDebouncedSave();
    }

    [Fact]
    public void Instance_WhenLoadedFromFile_DoesNotAutoSaveDuringLoad()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        LoadingAutoSaveSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, LoadingAutoSaveSettings.SettingsFileName);
        File.WriteAllText(filePath, """
            {
              "Name": "loaded"
            }
            """);

        var settings = LoadingAutoSaveSettings.Instance;

        Assert.Equal("normalized", settings.Name);
        Assert.Contains("\"loaded\"", File.ReadAllText(filePath));
    }

    private sealed class AutoSaveSettings : RootSettingsFile<AutoSaveSettings>
    {
        private string _name = string.Empty;

        public override string DirectoryPath => DirectoryPathOverride;

        public string DirectoryPathOverride { get; init; } = string.Empty;

        public override string FileName { get; } = $"{Guid.NewGuid():N}.json";

        protected override int DefaultDebounceSaveMilliseconds => 0;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public void EnableSaving()
        {
            CanSave = true;
            MarkLoaded(this);
        }
    }

    private sealed class DelayedAutoSaveSettings : RootSettingsFile<DelayedAutoSaveSettings>
    {
        private string _name = string.Empty;

        public int DebounceMilliseconds => 200;

        public int SaveCount { get; private set; }

        public override string DirectoryPath => DirectoryPathOverride;

        public string DirectoryPathOverride { get; init; } = string.Empty;

        public override string FileName { get; } = $"{Guid.NewGuid():N}.json";

        protected override int DefaultDebounceSaveMilliseconds => DebounceMilliseconds;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public void EnableSaving()
        {
            CanSave = true;
            MarkLoaded(this);
        }

        protected override void AfterSave()
        {
            SaveCount++;
        }
    }

    private sealed class LoadingAutoSaveSettings : RootSettingsFile<LoadingAutoSaveSettings>
    {
        private string _name = string.Empty;

        public const string SettingsFileName = "settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public LoadingAutoSaveSettings()
        {
            AutoSave = true;
        }

        public override string DirectoryPath => DirectoryPathOverride;

        public override string FileName => SettingsFileName;

        protected override int DefaultDebounceSaveMilliseconds => 0;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        protected override void OnLoaded(bool fromFile)
        {
            if (fromFile)
            {
                Name = "normalized";
            }
        }
    }

    private static void MarkLoaded(SubSettings settings)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        typeof(SubSettings).GetField("<IsLoaded>k__BackingField", flags)!.SetValue(settings, true);
    }
}
