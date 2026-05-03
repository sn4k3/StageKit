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
    public void Save_WhenFileMissingAndNoChanges_CreatesFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPathOverride = directoryPath
        };
        settings.EnableSaving();
        settings.ClearDirty();

        settings.Save();

        Assert.True(File.Exists(settings.FilePath));
        settings.DeleteFile();
    }

    [Fact]
    public void Save_WhenCollectionTrimDisabled_SavesItems()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new NoTrimCollectionSettings
        {
            DirectoryPathOverride = directoryPath
        };
        settings.EnableSaving();

        settings.Add(1);
        settings.Save();

        Assert.True(File.Exists(settings.FilePath));
        Assert.Contains("1", File.ReadAllText(settings.FilePath));
        settings.DeleteFile();
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

    [Fact]
    public void SubSettingsPropertyChanged_WhenAutoSaveEnabled_SavesRootFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        NestedAutoSaveSettings.DirectoryPathOverride = directoryPath;
        var settings = NestedAutoSaveSettings.Instance;

        settings.Child.Name = "nested";

        Assert.True(File.Exists(settings.FilePath));
        Assert.Contains("nested", File.ReadAllText(settings.FilePath));
        Assert.False(settings.HasUnsavedChanges);
        settings.DeleteFile();
    }

    [Fact]
    public void Save_AfterSuccess_LeavesNoTempFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPathOverride = directoryPath
        };
        settings.EnableSaving();

        settings.Name = "atomic";
        settings.Save();

        Assert.True(File.Exists(settings.FilePath));
        Assert.False(File.Exists(settings.FilePath + ".tmp"));
        settings.DeleteFile();
    }

    [Fact]
    public void Instance_WhenFileCorrupt_RenamesToBackupAndCreatesFreshInstance()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        CorruptLoadSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, CorruptLoadSettings.SettingsFileName);
        File.WriteAllText(filePath, "{ this is not valid json");

        var settings = CorruptLoadSettings.Instance;

        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.Name);

        var backups = Directory.GetFiles(directoryPath, $"{CorruptLoadSettings.SettingsFileName}.corrupt-*");
        Assert.Single(backups);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task WaitForDebouncedSaveAsync_WhenCancelDebouncedSave_ReturnsTrue()
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

        var waitTask = settings.WaitForDebouncedSaveAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        settings.CancelDebouncedSave();

        var completed = await waitTask;

        Assert.True(completed);
        Assert.False(settings.IsDebounceSavePending);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void Save_WhenTrimRemovesTrackedItems_DoesNotMarkDirtyWhenStaleItemsChange()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new TrimTrackingSettings
        {
            DirectoryPathOverride = directoryPath
        };
        settings.EnableSaving();

        var item1 = new TrackingItem();
        var item2 = new TrackingItem();
        var item3 = new TrackingItem();
        settings.Add(item1);
        settings.Add(item2);
        settings.Add(item3);

        settings.Save();
        settings.ClearDirty();

        Assert.Equal(2, settings.Count);
        Assert.DoesNotContain(item1, settings);

        item1.RaisePropertyChanged();

        Assert.False(settings.HasUnsavedChanges);
        settings.DeleteFile();
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

        public void ClearDirty()
        {
            ClearUnsavedChangesCore();
        }
    }

    private sealed class NoTrimCollectionSettings : RootCollectionFile<NoTrimCollectionSettings, int>
    {
        public override string DirectoryPath => DirectoryPathOverride;

        public string DirectoryPathOverride { get; init; } = string.Empty;

        public override string FileName { get; } = $"{Guid.NewGuid():N}.json";

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

    private sealed class NestedAutoSaveSettings : RootSettingsFile<NestedAutoSaveSettings>
    {
        public const string SettingsFileName = "nested-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public NestedAutoSaveSettings()
        {
            AutoSave = true;
        }

        public override string DirectoryPath => DirectoryPathOverride;

        public override string FileName => SettingsFileName;

        public ChildSettings Child { get; set; } = new();

        public override SubSettings[] SubSettingsCollection => [Child];

        protected override int DefaultDebounceSaveMilliseconds => 0;
    }

    private sealed class ChildSettings : SubSettings
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }

    private sealed class CorruptLoadSettings : RootSettingsFile<CorruptLoadSettings>
    {
        public const string SettingsFileName = "corrupt-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public override string DirectoryPath => DirectoryPathOverride;

        public override string FileName => SettingsFileName;

        protected override int DefaultDebounceSaveMilliseconds => 0;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class TrackingItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged() =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RaisePropertyChanged)));
    }

    private sealed class TrimTrackingSettings : RootCollectionFile<TrimTrackingSettings, TrackingItem>
    {
        public override string DirectoryPath => DirectoryPathOverride;

        public string DirectoryPathOverride { get; init; } = string.Empty;

        public override string FileName { get; } = $"{Guid.NewGuid():N}.json";

        public override int TrimCollectionWhenExceeding => 2;

        public override CollectionSide TrimCollectionSide => CollectionSide.Head;

        public override bool TrackItemsWithChangeNotification => true;

        protected override int DefaultDebounceSaveMilliseconds => 0;

        public void EnableSaving()
        {
            CanSave = true;
            MarkLoaded(this);
        }

        public void ClearDirty() => ClearUnsavedChangesCore();
    }

    private static void MarkLoaded(SubSettings settings)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        typeof(SubSettings).GetField("<IsLoaded>k__BackingField", flags)!.SetValue(settings, true);
    }
}
