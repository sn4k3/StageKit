namespace StageKit.Tests;

public sealed class RootSettingsFileAutoSaveTests
{
    [Fact]
    public void PropertyChanged_WhenAutoSaveEnabled_SavesFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPath = directoryPath,
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
            DirectoryPath = directoryPath,
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
            DirectoryPath = directoryPath
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
            DirectoryPath = directoryPath
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
            DirectoryPath = directoryPath,
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
            DirectoryPath = directoryPath,
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
            DirectoryPath = directoryPath,
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
            DirectoryPath = directoryPath,
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
              "SettingsVersion": 1,
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
            DirectoryPath = directoryPath
        };
        settings.EnableSaving();

        settings.Name = "atomic";
        settings.Save();

        Assert.True(File.Exists(settings.FilePath));
        Assert.False(File.Exists(settings.FilePath + ".tmp"));
        Assert.Equal(1, settings.SaveCount);
        Assert.DoesNotContain(nameof(RootSettingsFile<AutoSaveSettings>.SaveCount), File.ReadAllText(settings.FilePath));
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
            DirectoryPath = directoryPath,
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
            DirectoryPath = directoryPath
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

    [Fact]
    public void Save_WhenTrimRunsWithAutoSaveEnabled_DoesNotScheduleSecondSave()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new TrimTrackingSettings
        {
            DirectoryPath = directoryPath
        };
        settings.EnableSaving();

        settings.Add(new TrackingItem());
        settings.Add(new TrackingItem());
        settings.Add(new TrackingItem());
        settings.AutoSave = true;

        settings.Save();

        Assert.Equal(1, settings.SaveCount);
        Assert.False(settings.IsDebounceSavePending);
        Assert.False(settings.HasUnsavedChanges);
        settings.DeleteFile();
    }

    [Fact]
    public void Remove_WhenDuplicateTrackedItemStillExists_KeepsItemChangeTracking()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new TrimTrackingSettings
        {
            DirectoryPath = directoryPath,
            TrimCollectionWhenExceeding = 0
        };
        settings.EnableSaving();

        var item = new TrackingItem();
        settings.Add(item);
        settings.Add(item);
        settings.ClearDirty();

        settings.RemoveAt(0);
        settings.ClearDirty();
        Assert.Single(settings);

        item.RaisePropertyChanged();

        Assert.True(settings.HasUnsavedChanges);

        settings.ClearDirty();
        settings.RemoveAt(0);
        settings.ClearDirty();
        Assert.Empty(settings);

        item.RaisePropertyChanged();

        Assert.False(settings.HasUnsavedChanges);
    }

    [Fact]
    public void Instance_WhenSettingsVersionIsOlder_RunsMigrationAndPreservesDirtyState()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        MigrationSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, MigrationSettings.SettingsFileName);
        File.WriteAllText(filePath, """
            {
              "SettingsVersion": 1,
              "Name": "old"
            }
            """);

        var settings = MigrationSettings.Instance;

        Assert.Equal(2, settings.SettingsVersion);
        Assert.Equal("migrated", settings.Name);
        Assert.True(settings.MigrationRan);
        Assert.True(settings.HasUnsavedChanges);
        settings.DeleteFile();
    }

    [Fact]
    public void Instance_WhenSettingsVersionIsCurrent_DoesNotRunMigration()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        CurrentVersionSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, CurrentVersionSettings.SettingsFileName);
        File.WriteAllText(filePath, """
            {
              "SettingsVersion": 2,
              "Name": "current"
            }
            """);

        var settings = CurrentVersionSettings.Instance;

        Assert.Equal(2, settings.SettingsVersion);
        Assert.Equal("current", settings.Name);
        Assert.False(settings.MigrationRan);
        Assert.False(settings.HasUnsavedChanges);
        settings.DeleteFile();
    }

    [Fact]
    public void Instance_WhenSettingsVersionIsFuture_RenamesFileAndCreatesFreshInstance()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        FutureVersionSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, FutureVersionSettings.SettingsFileName);
        File.WriteAllText(filePath, """
            {
              "SettingsVersion": 99,
              "Name": "future"
            }
            """);

        var settings = FutureVersionSettings.Instance;

        Assert.Equal(1, settings.SettingsVersion);
        Assert.Equal(string.Empty, settings.Name);
        Assert.False(File.Exists(filePath));
        Assert.Single(Directory.GetFiles(directoryPath, $"{FutureVersionSettings.SettingsFileName}.unsupported-version-*"));
    }

    [Fact]
    public void Instance_WhenValidationRepairsSettings_MarksDirtyAndAutoSaves()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        ValidationRepairSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, ValidationRepairSettings.SettingsFileName);
        File.WriteAllText(filePath, """
            {
              "SettingsVersion": 1,
              "Theme": ""
            }
            """);

        var settings = ValidationRepairSettings.Instance;

        Assert.Equal("System", settings.Theme);
        Assert.False(settings.HasUnsavedChanges);
        Assert.Contains("\"System\"", File.ReadAllText(filePath));
        settings.DeleteFile();
    }

    [Fact]
    public void Instance_WhenValidationDoesNotRepairSettings_LeavesClean()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        ValidationCleanSettings.DirectoryPathOverride = directoryPath;
        var filePath = Path.Combine(directoryPath, ValidationCleanSettings.SettingsFileName);
        File.WriteAllText(filePath, """
            {
              "SettingsVersion": 1,
              "Theme": "Dark"
            }
            """);

        var settings = ValidationCleanSettings.Instance;

        Assert.Equal("Dark", settings.Theme);
        Assert.False(settings.HasUnsavedChanges);
        settings.DeleteFile();
    }

    [Fact]
    public void SuspendAutoSave_WhenPropertyChanges_DelaysSaveUntilDisposed()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPath = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        using (settings.SuspendAutoSave())
        {
            settings.Name = "batched";
            Assert.False(File.Exists(settings.FilePath));
        }

        Assert.True(File.Exists(settings.FilePath));
        Assert.Contains("batched", File.ReadAllText(settings.FilePath));
        settings.DeleteFile();
    }

    [Fact]
    public void SuspendAutoSave_WhenNested_SavesOnlyAfterOutermostDispose()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new DelayedAutoSaveSettings
        {
            DirectoryPath = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        using (settings.SuspendAutoSave())
        {
            using (settings.SuspendAutoSave())
            {
                settings.Name = "nested";
            }

            Assert.False(settings.IsDebounceSavePending);
        }

        Assert.True(settings.IsDebounceSavePending);
        settings.CancelDebouncedSave();
    }

    [Fact]
    public void SuspendAutoSave_WhenSaveOnDisposeFalse_LeavesDirtyWithoutSchedulingSave()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new DelayedAutoSaveSettings
        {
            DirectoryPath = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        using (settings.SuspendAutoSave(saveOnDispose: false))
        {
            settings.Name = "dirty";
        }

        Assert.True(settings.HasUnsavedChanges);
        Assert.False(settings.IsDebounceSavePending);
        Assert.False(File.Exists(settings.FilePath));
    }

    [Fact]
    public void SuspendAutoSave_WhenAutoSaveEnabledBeforeDispose_SavesSuspendedChanges()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPath = directoryPath,
            AutoSave = false
        };
        settings.EnableSaving();

        using (settings.SuspendAutoSave())
        {
            settings.Name = "enabled-later";
            settings.AutoSave = true;
        }

        Assert.True(File.Exists(settings.FilePath));
        Assert.Contains("enabled-later", File.ReadAllText(settings.FilePath));
        settings.DeleteFile();
    }

    [Fact]
    public void BatchUpdate_WhenActionThrows_ResumesAutoSave()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var settings = new AutoSaveSettings
        {
            DirectoryPath = directoryPath,
            AutoSave = true
        };
        settings.EnableSaving();

        Assert.Throws<InvalidOperationException>(() => settings.BatchUpdate(() =>
        {
            settings.Name = "before-throw";
            throw new InvalidOperationException();
        }, saveOnComplete: false));

        Assert.False(File.Exists(settings.FilePath));

        settings.Name = "after-throw";

        Assert.True(File.Exists(settings.FilePath));
        Assert.Contains("after-throw", File.ReadAllText(settings.FilePath));
        settings.DeleteFile();
    }

    private sealed class AutoSaveSettings : RootSettingsFile<AutoSaveSettings>
    {
        private string _name = string.Empty;

        public AutoSaveSettings()
        {
            FileName = $"{Guid.NewGuid():N}.json";
            DefaultDebounceSaveMilliseconds = 0;
        }

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
        public NoTrimCollectionSettings()
        {
            FileName = $"{Guid.NewGuid():N}.json";
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

        public DelayedAutoSaveSettings()
        {
            FileName = $"{Guid.NewGuid():N}.json";
            DefaultDebounceSaveMilliseconds = DebounceMilliseconds;
        }

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

    private sealed class LoadingAutoSaveSettings : RootSettingsFile<LoadingAutoSaveSettings>
    {
        private string _name = string.Empty;

        public const string SettingsFileName = "settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public LoadingAutoSaveSettings()
        {
            AutoSave = true;
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

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
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public ChildSettings Child { get; set; } = new();

        public override SubSettings[] SubSettingsCollection => [Child];

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

        public CorruptLoadSettings()
        {
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

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
        public TrimTrackingSettings()
        {
            FileName = $"{Guid.NewGuid():N}.json";
            TrimCollectionWhenExceeding = 2;
            TrimCollectionSide = CollectionSide.Head;
            TrackItemsWithChangeNotification = true;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public void EnableSaving()
        {
            CanSave = true;
            MarkLoaded(this);
        }

        public void ClearDirty() => ClearUnsavedChangesCore();
    }

    private sealed class MigrationSettings : RootSettingsFile<MigrationSettings>
    {
        public const string SettingsFileName = "migration-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public MigrationSettings()
        {
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public string Name { get; set; } = string.Empty;

        public bool MigrationRan { get; private set; }

        protected override int CurrentSettingsVersion => 2;

        protected override void MigrateSettings(SettingsMigrationContext context)
        {
            MigrationRan = true;
            Name = "migrated";
        }
    }

    private sealed class CurrentVersionSettings : RootSettingsFile<CurrentVersionSettings>
    {
        public const string SettingsFileName = "current-version-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public CurrentVersionSettings()
        {
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public string Name { get; set; } = string.Empty;

        public bool MigrationRan { get; private set; }

        protected override int CurrentSettingsVersion => 2;

        protected override void MigrateSettings(SettingsMigrationContext context)
        {
            MigrationRan = true;
        }
    }

    private sealed class FutureVersionSettings : RootSettingsFile<FutureVersionSettings>
    {
        public const string SettingsFileName = "future-version-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public FutureVersionSettings()
        {
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ValidationRepairSettings : RootSettingsFile<ValidationRepairSettings>
    {
        public const string SettingsFileName = "validation-repair-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public ValidationRepairSettings()
        {
            AutoSave = true;
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public string Theme { get; set; } = "System";

        protected override void ValidateSettings(SettingsValidationContext context)
        {
            if (!string.IsNullOrWhiteSpace(Theme)) return;
            Theme = "System";
            context.MarkChanged("Theme was empty.");
        }
    }

    private sealed class ValidationCleanSettings : RootSettingsFile<ValidationCleanSettings>
    {
        public const string SettingsFileName = "validation-clean-settings.json";

        public static string DirectoryPathOverride { get; set; } = string.Empty;

        public ValidationCleanSettings()
        {
            DirectoryPath = DirectoryPathOverride;
            FileName = SettingsFileName;
            DefaultDebounceSaveMilliseconds = 0;
        }

        public string Theme { get; set; } = "System";

        protected override void ValidateSettings(SettingsValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(Theme))
            {
                Theme = "System";
                context.MarkChanged("Theme was empty.");
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
