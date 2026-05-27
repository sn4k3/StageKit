using System.IO.Compression;
using StageKit.Primitives;

namespace StageKit.Tests;

public sealed class ApplicationStorageUtilitiesTests
{
    [Fact]
    public void SafeFile_WriteAllText_ReplacesExistingFile()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        File.WriteAllText(filePath, "old");

        SafeFile.WriteAllText(filePath, "new");

        Assert.Equal("new", File.ReadAllText(filePath));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public async Task SafeFile_WriteAllTextAsync_ReplacesExistingFile()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        await File.WriteAllTextAsync(filePath, "old", TestContext.Current.CancellationToken);

        await SafeFile.WriteAllTextAsync(filePath, "new", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("new", await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public async Task SafeFile_WriteAsync_WhenCanceled_PreservesExistingFile()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        await File.WriteAllTextAsync(filePath, "old", TestContext.Current.CancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await SafeFile.WriteAsync(
                filePath,
                async (stream, token) =>
                {
                    await stream.WriteAsync("new"u8.ToArray(), TestContext.Current.CancellationToken);
                    await cancellationTokenSource.CancelAsync();
                    token.ThrowIfCancellationRequested();
                },
                cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        Assert.Equal("old", await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public void ApplicationBackup_CreateAndRestore_RestoresFiles()
    {
        var sourcePath = CreateTempDirectory();
        var restorePath = CreateTempDirectory();
        var backupPath = Path.Combine(CreateTempDirectory(), "backup.zip");
        Directory.CreateDirectory(Path.Combine(sourcePath, "configs"));
        File.WriteAllText(Path.Combine(sourcePath, "configs", "app.json"), "value");

        var createdPath = ApplicationBackup.Create(new ApplicationBackupOptions
        {
            SourceDirectoryPath = sourcePath,
            DestinationFilePath = backupPath,
        });

        ApplicationBackup.Restore(createdPath, restorePath);

        Assert.True(File.Exists(Path.Combine(restorePath, "configs", "app.json")));
        Assert.Equal("value", File.ReadAllText(Path.Combine(restorePath, "configs", "app.json")));
    }

    [Fact]
    public async Task ApplicationBackup_CreateAndRestoreAsync_RestoresFiles()
    {
        var sourcePath = CreateTempDirectory();
        var restorePath = CreateTempDirectory();
        var backupPath = Path.Combine(CreateTempDirectory(), "backup.zip");
        Directory.CreateDirectory(Path.Combine(sourcePath, "configs"));
        await File.WriteAllTextAsync(
            Path.Combine(sourcePath, "configs", "app.json"),
            "value",
            TestContext.Current.CancellationToken);

        var createdPath = await ApplicationBackup.CreateAsync(
            new ApplicationBackupOptions
            {
                SourceDirectoryPath = sourcePath,
                DestinationFilePath = backupPath,
            },
            TestContext.Current.CancellationToken);

        await ApplicationBackup.RestoreAsync(
            createdPath,
            restorePath,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(restorePath, "configs", "app.json")));
        Assert.Equal("value", await File.ReadAllTextAsync(
            Path.Combine(restorePath, "configs", "app.json"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SupportBundleExporter_Export_IncludesManifestConfigsAndLogs()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var profilePath = CreateTempDirectory();
        var bundlePath = Path.Combine(CreateTempDirectory(), "support.zip");

        try
        {
            ApplicationKit.ProfilePath = profilePath;
            Directory.CreateDirectory(ApplicationKit.ConfigsPath);
            Directory.CreateDirectory(ApplicationKit.LogsPath);
            File.WriteAllText(Path.Combine(ApplicationKit.ConfigsPath, "settings.json"), "config");
            File.WriteAllText(Path.Combine(ApplicationKit.LogsPath, "app.log"), "log");

            SupportBundleExporter.Export(new SupportBundleOptions
            {
                DestinationFilePath = bundlePath,
                Notes = "test",
            });

            using var archive = ZipFile.OpenRead(bundlePath);
            var manifestEntry = archive.GetEntry("manifest.json");
            Assert.NotNull(manifestEntry);
            using (var manifestStream = manifestEntry.Open())
            using (var reader = new StreamReader(manifestStream))
            {
                var manifest = reader.ReadToEnd();
                Assert.Contains("\"ApplicationName\"", manifest);
                Assert.Contains("\"Notes\": \"test\"", manifest);
            }

            Assert.NotNull(archive.GetEntry("configs/settings.json"));
            Assert.NotNull(archive.GetEntry("logs/app.log"));
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public async Task SupportBundleExporter_ExportAsync_IncludesManifestConfigsAndLogs()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var profilePath = CreateTempDirectory();
        var bundlePath = Path.Combine(CreateTempDirectory(), "support.zip");

        try
        {
            ApplicationKit.ProfilePath = profilePath;
            Directory.CreateDirectory(ApplicationKit.ConfigsPath);
            Directory.CreateDirectory(ApplicationKit.LogsPath);
            await File.WriteAllTextAsync(
                Path.Combine(ApplicationKit.ConfigsPath, "settings.json"),
                "config",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(ApplicationKit.LogsPath, "app.log"),
                "log",
                TestContext.Current.CancellationToken);

            await SupportBundleExporter.ExportAsync(
                new SupportBundleOptions
                {
                    DestinationFilePath = bundlePath,
                    Notes = "test",
                },
                TestContext.Current.CancellationToken);

            using var archive = ZipFile.OpenRead(bundlePath);
            var manifestEntry = archive.GetEntry("manifest.json");
            Assert.NotNull(manifestEntry);
            using (var manifestStream = manifestEntry.Open())
            using (var reader = new StreamReader(manifestStream))
            {
                var manifest = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                Assert.Contains("\"ApplicationName\"", manifest);
                Assert.Contains("\"Notes\": \"test\"", manifest);
            }

            Assert.NotNull(archive.GetEntry("configs/settings.json"));
            Assert.NotNull(archive.GetEntry("logs/app.log"));
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public void SupportBundleExporter_Export_WhenDestinationIsUnderLogs_DoesNotIncludeItself()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var profilePath = CreateTempDirectory();

        try
        {
            ApplicationKit.ProfilePath = profilePath;
            Directory.CreateDirectory(ApplicationKit.LogsPath);
            File.WriteAllText(Path.Combine(ApplicationKit.LogsPath, "app.log"), "log");
            var bundlePath = Path.Combine(ApplicationKit.LogsPath, "support.zip");

            SupportBundleExporter.Export(new SupportBundleOptions
            {
                DestinationFilePath = bundlePath,
                IncludeConfigs = false,
                IncludeLogs = true,
            });

            using var archive = ZipFile.OpenRead(bundlePath);

            Assert.NotNull(archive.GetEntry("logs/app.log"));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("support.zip", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains(".tmp.", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public async Task SupportBundleExporter_ExportAsync_WhenDestinationIsUnderLogs_DoesNotIncludeItself()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var profilePath = CreateTempDirectory();

        try
        {
            ApplicationKit.ProfilePath = profilePath;
            Directory.CreateDirectory(ApplicationKit.LogsPath);
            await File.WriteAllTextAsync(
                Path.Combine(ApplicationKit.LogsPath, "app.log"),
                "log",
                TestContext.Current.CancellationToken);
            var bundlePath = Path.Combine(ApplicationKit.LogsPath, "support.zip");

            await SupportBundleExporter.ExportAsync(
                new SupportBundleOptions
                {
                    DestinationFilePath = bundlePath,
                    IncludeConfigs = false,
                    IncludeLogs = true,
                },
                TestContext.Current.CancellationToken);

            using var archive = ZipFile.OpenRead(bundlePath);

            Assert.NotNull(archive.GetEntry("logs/app.log"));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("support.zip", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains(".tmp.", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public void ApplicationRetention_ApplyFileRetention_DeletesOldFilesAndKeepsNewest()
    {
        var directoryPath = CreateTempDirectory();
        var oldPath = Path.Combine(directoryPath, "old.log");
        var newestPath = Path.Combine(directoryPath, "newest.log");
        var secondNewestPath = Path.Combine(directoryPath, "second.log");
        File.WriteAllText(oldPath, "old");
        File.WriteAllText(secondNewestPath, "second");
        File.WriteAllText(newestPath, "newest");
        File.SetLastWriteTimeUtc(oldPath, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(secondNewestPath, DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(newestPath, DateTime.UtcNow.AddMinutes(-1));

        var result = ApplicationRetention.ApplyFileRetention(directoryPath, new FileRetentionPolicy
        {
            MaxAge = TimeSpan.FromDays(5),
            MaxFiles = 1,
            SearchPattern = "*.log",
        });

        Assert.Equal(2, result.DeletedCount);
        Assert.False(File.Exists(oldPath));
        Assert.False(File.Exists(secondNewestPath));
        Assert.True(File.Exists(newestPath));
    }

    [Fact]
    public void CrashReportsFile_ApplyRetention_RemovesOldAndExcessReports()
    {
        var file = new TestCrashReportsFile
        {
            DirectoryPath = CreateTempDirectory(),
            FileName = $"{Guid.NewGuid():N}.json",
            AutoSave = false,
            TrimCollectionWhenExceeding = 0
        };
        file.EnableSaving();
        var oldReport = new CrashReport(new InvalidOperationException("old"), "test", DateTime.UtcNow.AddDays(-10));
        var keptReport = new CrashReport(new InvalidOperationException("kept"), "test", DateTime.UtcNow.AddMinutes(-1));
        var excessReport = new CrashReport(new InvalidOperationException("excess"), "test", DateTime.UtcNow.AddMinutes(-2));
        file.Add(oldReport);
        file.Add(excessReport);
        file.Add(keptReport);

        var removed = file.ApplyRetention(maxCrashReports: 1, maxAge: TimeSpan.FromDays(5));

        Assert.Equal(2, removed);
        Assert.Single(file);
        Assert.Same(keptReport, file[0]);
        Assert.Equal(1, file.SaveCount);
    }

    [Fact]
    public async Task OnboardingStateFile_Instance_TracksLaunchAndOnboardingState()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var originalFileName = OnboardingStateFile.OnboardingStateFileName;
        var profilePath = CreateTempDirectory();

        try
        {
            ApplicationKit.ProfilePath = profilePath;
            OnboardingStateFile.OnboardingStateFileName = $"{Guid.NewGuid():N}.json";

            var state = OnboardingStateFile.Instance;
            Assert.True(state.IsFirstRun);

            state.RecordLaunch();
            state.RecordLaunch();
            state.CompleteOnboarding("v1");

            Assert.Equal(2, state.LaunchCount);
            Assert.NotNull(state.LastLaunchUtc);
            Assert.False(state.IsFirstRun);
            Assert.Equal("v1", state.OnboardingVersion);
            Assert.True(await state.WaitForDebouncedSaveAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Contains("\"OnboardingVersion\": \"v1\"", File.ReadAllText(state.FilePath));

            state.ResetOnboarding();

            Assert.True(state.IsFirstRun);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
            OnboardingStateFile.OnboardingStateFileName = originalFileName;
        }
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private sealed class TestCrashReportsFile : CrashReportsFile
    {
        public void EnableSaving()
        {
            CanSave = true;
            OnLoadedCore(fromFile: false);
        }
    }
}
