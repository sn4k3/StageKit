using System.Net;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Octokit;
using StageKit.Runtime;

namespace StageKit.Updatum.Tests;

public sealed class UpdatumManagerTests
{
    [Fact]
    public void ResolveExtractedMacOSAppBundlePath_SelectsOnlyTopLevelAppBundle()
    {
        var extractionRoot = Directory.CreateTempSubdirectory("StageKit.Updatum.MacZip-").FullName;
        try
        {
            var expectedBundlePath = Directory.CreateDirectory(Path.Combine(extractionRoot, "Test App.app")).FullName;
            Directory.CreateDirectory(Path.Combine(
                expectedBundlePath,
                "Contents",
                "Frameworks",
                "Helper.app"));

            var result = UpdatumManager.ResolveExtractedMacOSAppBundlePath(extractionRoot);

            Assert.Equal(expectedBundlePath, result);
        }
        finally
        {
            Directory.Delete(extractionRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, "does not contain a top-level app bundle")]
    [InlineData(2, "contains multiple top-level app bundles")]
    public void ResolveExtractedMacOSAppBundlePath_RejectsAmbiguousArchive(
        int appBundleCount,
        string expectedMessage)
    {
        var extractionRoot = Directory.CreateTempSubdirectory("StageKit.Updatum.MacZip-").FullName;
        try
        {
            for (var index = 0; index < appBundleCount; index++)
            {
                Directory.CreateDirectory(Path.Combine(extractionRoot, $"Test App {index}.app"));
            }

            var exception = Assert.Throws<InvalidDataException>(() =>
                UpdatumManager.ResolveExtractedMacOSAppBundlePath(extractionRoot));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(extractionRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_RestoresIdleState_WhenReleaseHasNoCompatibleAsset()
    {
        using var manager = CreateManager();
        var release = CreateRelease([]);

        var result = await manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(UpdatumState.None, manager.State);
        Assert.False(manager.IsBusy);
    }

    [Fact]
    public async Task DownloadUpdateAsync_UsesUniqueContainedWorkspace_AndVerifiesChecksum()
    {
        var payload = Encoding.UTF8.GetBytes("verified update");
        var checksum = Convert.ToHexString(SHA256.HashData(payload));
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{checksum}  app.zip") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = CreateManager(httpClient);
        manager.RequireAssetChecksum = true;
        manager.RequireAssetSignatureVerification = true;
        manager.AssetSignatureVerifier = (_, _) => ValueTask.FromResult(true);
        var release = CreateRelease(
        [
            CreateAsset("app.zip.sha256", "https://example.test/app.zip.sha256", checksum.Length),
            CreateAsset("app.zip", "https://example.test/app.zip", payload.Length)
        ]);

        var first = await manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken);
        var second = await manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken);

        try
        {
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.True(first.IsChecksumVerified);
            Assert.True(first.IsSignatureVerified);
            Assert.Equal(checksum, first.Sha256, ignoreCase: true);
            Assert.NotEqual(Path.GetDirectoryName(first.FilePath), Path.GetDirectoryName(second.FilePath));
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(first.FilePath), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("app.zip", first.FileName);
        }
        finally
        {
            first?.SafeDeleteFile();
            second?.SafeDeleteFile();
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_PrefersGitHubDigest_WithoutDownloadingSidecar()
    {
        var payload = Encoding.UTF8.GetBytes("GitHub-verified update");
        var checksum = Convert.ToHexString(SHA256.HashData(payload));
        var sidecarRequested = false;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal))
            {
                sidecarRequested = true;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('0', 64)) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        }));
        using var manager = CreateManager(httpClient);
        manager.RequireAssetChecksum = true;
        manager.GitHubAssetDigest = $"sha256:{checksum.ToLowerInvariant()}";
        var release = CreateRelease(
        [
            CreateAsset("app.zip", "https://example.test/app.zip", payload.Length),
            CreateAsset("app.zip.sha256", "https://example.test/app.zip.sha256", 64)
        ]);

        var download = await manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken);

        try
        {
            Assert.NotNull(download);
            Assert.True(download.IsChecksumVerified);
            Assert.Equal(checksum, download.Sha256);
            Assert.Equal(1, manager.GitHubDigestRequestCount);
            Assert.False(sidecarRequested);
        }
        finally
        {
            download?.SafeDeleteFile();
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_RejectsGitHubDigestMismatch_AndRestoresIdleState()
    {
        var payload = Encoding.UTF8.GetBytes("tampered GitHub asset");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = CreateManager(httpClient);
        manager.RequireAssetChecksum = true;
        manager.GitHubAssetDigest = $"sha256:{new string('0', 64)}";
        var release = CreateRelease([CreateAsset("app.zip", "https://example.test/app.zip", payload.Length)]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));

        Assert.Equal(1, manager.GitHubDigestRequestCount);
        Assert.Equal(UpdatumState.None, manager.State);
        Assert.False(manager.IsBusy);
    }

    [Fact]
    public async Task DownloadUpdateAsync_ReadsGitHubDigestThroughOctokitConnection()
    {
        var payload = Encoding.UTF8.GetBytes("Octokit metadata update");
        var checksum = Convert.ToHexString(SHA256.HashData(payload));
        var metadataRequestCount = 0;
        var octokitHttpClient = new StubOctokitHttpClient(request =>
        {
            metadataRequestCount++;
            Assert.Equal(
                new Uri("https://api.github.com/repos/owner/repository/releases/assets/1"),
                new Uri(request.BaseAddress, request.Endpoint));
            return new StubOctokitResponse($$"""{"digest":"sha256:{{checksum}}"}""");
        });
        var githubClient = new GitHubClient(new Connection(new ProductHeaderValue("StageKit.Updatum.Tests"), octokitHttpClient));
        using var assetHttpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = new UpdatumManager("owner", "repository", githubClient)
        {
            AssetRegexPattern = string.Empty,
            AssetHttpClient = assetHttpClient,
            RequireAssetChecksum = true
        };
        var release = CreateRelease(
        [
            CreateAsset(
                "app.zip",
                "https://api.github.com/repos/owner/repository/releases/assets/1",
                payload.Length,
                "https://downloads.example.test/app.zip")
        ]);

        var download = await manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken);

        try
        {
            Assert.NotNull(download);
            Assert.True(download.IsChecksumVerified);
            Assert.Equal(checksum, download.Sha256);
            Assert.Equal(1, metadataRequestCount);
        }
        finally
        {
            download?.SafeDeleteFile();
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_RejectsDigestMetadataOutsideGitHubApiOrigin()
    {
        var payload = Encoding.UTF8.GetBytes("untrusted metadata update");
        var metadataRequestCount = 0;
        var octokitHttpClient = new StubOctokitHttpClient(_ =>
        {
            metadataRequestCount++;
            return new StubOctokitResponse($$"""{"digest":"sha256:{{new string('0', 64)}}"}""");
        });
        var githubClient = new GitHubClient(new Connection(new ProductHeaderValue("StageKit.Updatum.Tests"), octokitHttpClient));
        using var assetHttpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = new UpdatumManager("owner", "repository", githubClient)
        {
            AssetRegexPattern = string.Empty,
            AssetHttpClient = assetHttpClient,
            RequireAssetChecksum = true
        };
        var release = CreateRelease(
        [
            CreateAsset(
                "app.zip",
                "https://attacker.example/assets/1",
                payload.Length,
                "https://downloads.example.test/app.zip")
        ]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));

        Assert.Equal(0, metadataRequestCount);
        Assert.Equal(UpdatumState.None, manager.State);
    }

    [Fact]
    public async Task DownloadUpdateAsync_RejectsFailedSignatureVerification_AndRestoresIdleState()
    {
        var payload = Encoding.UTF8.GetBytes("unsigned update");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = CreateManager(httpClient);
        manager.RequireAssetSignatureVerification = true;
        manager.AssetSignatureVerifier = (_, _) => ValueTask.FromResult(false);
        var release = CreateRelease([CreateAsset("app.zip", "https://example.test/app.zip", payload.Length)]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));

        Assert.Equal(UpdatumState.None, manager.State);
        Assert.False(manager.IsBusy);
    }

    [Fact]
    public async Task DownloadUpdateAsync_RejectsChecksumMismatch_AndRestoresIdleState()
    {
        var payload = Encoding.UTF8.GetBytes("tampered update");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('0', 64)) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = CreateManager(httpClient);
        manager.RequireAssetChecksum = true;
        var release = CreateRelease(
        [
            CreateAsset("app.zip", "https://example.test/app.zip", payload.Length),
            CreateAsset("app.zip.sha256", "https://example.test/app.zip.sha256", 64)
        ]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));

        Assert.Equal(UpdatumState.None, manager.State);
        Assert.False(manager.IsBusy);
    }

    [Theory]
    [InlineData("../app.zip")]
    [InlineData("folder/app.zip")]
    [InlineData("folder\\app.zip")]
    public async Task DownloadUpdateAsync_RejectsAssetNamesContainingPathComponents(string assetName)
    {
        using var manager = CreateManager();
        var release = CreateRelease([CreateAsset(assetName, "https://example.test/app.zip", 1)]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));

        Assert.Equal(UpdatumState.None, manager.State);
    }

    [Fact]
    public async Task InstallUpdateAsync_RemainsBusyUntilCoreCompletes()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            using var manager = new BlockingInstallManager();
            var downloadedAsset = CreateDownloadedAsset(filePath);

            var installTask = manager.InstallUpdateAsync(downloadedAsset, false, null, TestContext.Current.CancellationToken);
            await manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(UpdatumState.InstallingUpdate, manager.State);
            Assert.True(manager.IsBusy);
            Assert.False(await manager.InstallUpdateAsync(downloadedAsset, false, null, TestContext.Current.CancellationToken));

            manager.Complete.TrySetResult(true);
            Assert.True(await installTask);
            Assert.Equal(UpdatumState.None, manager.State);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task InstallUpdateAsync_CancellationRestoresIdleState()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            using var manager = new BlockingInstallManager();
            using var cancellationSource = new CancellationTokenSource();
            var downloadedAsset = CreateDownloadedAsset(filePath);
            var installTask = manager.InstallUpdateAsync(downloadedAsset, false, null, cancellationSource.Token);
            await manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installTask);
            Assert.Equal(UpdatumState.None, manager.State);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void PropertyChanged_IsDispatchedThroughConfiguredSynchronizationContext()
    {
        using var manager = CreateManager();
        var context = new QueuedSynchronizationContext();
        manager.EventSynchronizationContext = context;
        var notifications = new List<string?>();
        manager.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        manager.AssetExtensionFilter = "zip";

        Assert.Empty(notifications);
        context.Drain();
        Assert.Contains(nameof(UpdatumManager.AssetExtensionFilter), notifications);
    }

    [Fact]
    public void SelectReleasesAhead_UsesHighestEligibleVersionRegardlessOfResponseOrder()
    {
        using var manager = CreateManager();
        var asset = CreateAsset("app.zip", "https://example.test/app.zip", 1);
        var selected = manager.SelectReleasesAhead(
        [
            CreateRelease([asset], "v1.0.0"),
            CreateRelease([asset], "v3.0.0", draft: true),
            CreateRelease([asset], "v2.0.0"),
            CreateRelease([asset], "v4.0.0", prerelease: true),
            CreateRelease([asset], "v3.0.0")
        ], new Version(1, 0, 0));

        Assert.Equal(["v3.0.0", "v2.0.0"], selected.Select(release => release.TagName));
    }

    [Fact]
    public void GetCompatibleReleaseAsset_ExcludesUnsupportedTarCompression()
    {
        using var manager = CreateManager();
        var release = CreateRelease(
        [
            CreateAsset("app.tar.bz2", "https://example.test/app.tar.bz2", 1),
            CreateAsset("app.tar.xz", "https://example.test/app.tar.xz", 1),
            CreateAsset("app.tar.gz", "https://example.test/app.tar.gz", 1)
        ]);

        Assert.Equal("app.tar.gz", manager.GetCompatibleReleaseAsset(release)?.Name);
        Assert.Null(manager.GetCompatibleReleaseAsset(CreateRelease(release.Assets.Take(2).ToArray())));
    }

    [Fact]
    public void GetInstallPackagingTypeForFile_RoutesWindowsSingleFileExeToReplacement()
    {
        Assert.Equal(ApplicationPackagingType.DotNetSingleFile,
            UpdatumManager.GetInstallPackagingTypeForFile("app.exe", ApplicationPackagingType.DotNetSingleFile,
                UpdatumWindowsExeType.Auto, true));
        Assert.Equal(ApplicationPackagingType.DotNetSingleFile,
            UpdatumManager.GetInstallPackagingTypeForFile("app.exe", ApplicationPackagingType.WindowsInstaller,
                UpdatumWindowsExeType.SingleFileApp, true));
        Assert.Equal(ApplicationPackagingType.WindowsInstaller,
            UpdatumManager.GetInstallPackagingTypeForFile("app.exe", ApplicationPackagingType.DotNetSingleFile,
                UpdatumWindowsExeType.Installer, true));
    }

    [Fact]
    public void CreateLinuxRelaunchArguments_PreservesQuotedArguments()
    {
        Assert.Equal(["run", "com.example.App", "--name", "A B", "--flag", ""],
            UpdatumManager.CreateLinuxRelaunchArguments("com.example.App", "--name \"A B\" --flag \"\""));
    }

    [Fact]
    public async Task DownloadUpdateAsync_RejectsAssetSizeMismatchEvenWithMatchingHttpLength()
    {
        var payload = Encoding.UTF8.GetBytes("short");
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = CreateManager(client);
        var release = CreateRelease([CreateAsset("app.zip", "https://example.test/app.zip", payload.Length + 1)]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));
        Assert.Equal(UpdatumState.None, manager.State);
    }

    [Fact]
    public async Task InstallUpdateAsync_RejectsChangedFileBeforeInstallCoreRuns()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("original")));
            await File.WriteAllTextAsync(filePath, "changed", TestContext.Current.CancellationToken);
            using var manager = new BlockingInstallManager();
            var asset = CreateDownloadedAsset(filePath) with { ExpectedSha256 = expected };

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                manager.InstallUpdateAsync(asset, false, null, TestContext.Current.CancellationToken));
            Assert.False(manager.Started.Task.IsCompleted);
            Assert.Equal(UpdatumState.None, manager.State);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task InstallUpdateAsync_RequiresRecordedDigestWhenConfigured()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            using var manager = CreateManager();
            manager.RequireAssetChecksum = true;
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                manager.InstallUpdateAsync(CreateDownloadedAsset(filePath), false, null,
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task InstallCompletion_WaitsForPostedHandlerBeforeReturning()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            using var manager = CreateManager();
            var context = new QueuedSynchronizationContext();
            manager.EventSynchronizationContext = context;
            var invoked = false;
            manager.InstallUpdateCompleted += (_, _) => invoked = true;
            var notification = manager.RaiseInstallUpdateCompletedAsync(
                CreateDownloadedAsset(filePath), true, TestContext.Current.CancellationToken);

            Assert.False(notification.IsCompleted);
            Assert.False(invoked);
            context.Drain();
            await notification;
            Assert.True(invoked);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
    [Fact]
    public async Task DownloadUpdateAsync_StopsReadingOversizedChecksumSidecar()
    {
        var payload = Encoding.UTF8.GetBytes("update");
        var checksumStream = new CountingReadStream(new byte[10_000]);
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(checksumStream) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        using var manager = CreateManager(client);
        manager.RequireAssetChecksum = true;
        var release = CreateRelease(
        [
            CreateAsset("app.zip", "https://example.test/app.zip", payload.Length),
            CreateAsset("app.zip.sha256", "https://example.test/app.zip.sha256", 10_000)
        ]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.DownloadUpdateAsync(release, TestContext.Current.CancellationToken));
        Assert.InRange(checksumStream.BytesRead, 1, 4097);
    }

    [Theory]
    [InlineData(".tar")]
    [InlineData(".tar.gz")]
    public void ExtractTarArchive_ExtractsRegularFiles(string suffix)
    {
        var directory = Directory.CreateTempSubdirectory("StageKit.Updatum.Tar-").FullName;
        try
        {
            var archivePath = Path.Combine(directory, "update" + suffix);
            using (var file = File.Create(archivePath))
            using (Stream tarStream = suffix == ".tar.gz"
                       ? new GZipStream(file, CompressionLevel.Optimal, leaveOpen: true)
                       : file)
            using (var writer = new TarWriter(tarStream))
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, "app/app.bin")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes("contents"))
                };
                writer.WriteEntry(entry);
            }

            var extractionPath = Path.Combine(directory, "extracted");
            UpdatumManager.ExtractTarArchive(archivePath, extractionPath);
            Assert.Equal("contents", File.ReadAllText(Path.Combine(extractionPath, "app", "app.bin")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExtractTarArchive_RejectsTraversal()
    {
        var directory = Directory.CreateTempSubdirectory("StageKit.Updatum.Tar-").FullName;
        try
        {
            var archivePath = Path.Combine(directory, "update.tar");
            using (var file = File.Create(archivePath))
            using (var writer = new TarWriter(file))
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "../escape.bin")
                {
                    DataStream = new MemoryStream([1, 2, 3])
                });
            }

            Assert.Throws<InvalidDataException>(() =>
                UpdatumManager.ExtractTarArchive(archivePath, Path.Combine(directory, "extracted")));
            Assert.False(File.Exists(Path.Combine(directory, "escape.bin")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    private static TestUpdatumManager CreateManager(HttpClient? httpClient = null)
    {
        return new TestUpdatumManager
        {
            AssetRegexPattern = string.Empty,
            AssetHttpClient = httpClient ?? UpdatumManager.HttpClient
        };
    }

    private static UpdatumDownloadedAsset CreateDownloadedAsset(string filePath)
    {
        var asset = CreateAsset(Path.GetFileName(filePath), "https://example.test/update", checked((int)new FileInfo(filePath).Length));
        return new UpdatumDownloadedAsset(CreateRelease([asset]), asset, filePath);
    }

    private static Release CreateRelease(IReadOnlyList<ReleaseAsset> assets, string tag = "v2.0.0", bool draft = false, bool prerelease = false)
    {
        return new Release(
            "https://api.example.test/release",
            "https://example.test/release",
            "https://api.example.test/assets",
            "https://uploads.example.test/assets",
            1,
            "node",
            tag,
            "main",
            "Version 2.0.0",
            "Changes",
            draft,
            prerelease,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null!,
            "https://example.test/source.tar.gz",
            "https://example.test/source.zip",
            assets);
    }

    private static ReleaseAsset CreateAsset(string name, string url, int size, string? browserDownloadUrl = null)
    {
        return new ReleaseAsset(
            url,
            1,
            "node",
            name,
            string.Empty,
            "uploaded",
            "application/octet-stream",
            size,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            browserDownloadUrl ?? url,
            null!);
    }

    private sealed class BlockingInstallManager : UpdatumManager
    {
        [SetsRequiredMembers]
        public BlockingInstallManager() : base("owner", "repository")
        {
        }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal override async Task<bool> InstallUpdateCoreAsync(
            UpdatumDownloadedAsset downloadedAsset,
            bool forceTerminate,
            string? runArguments,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return await Complete.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class TestUpdatumManager : UpdatumManager
    {
        [SetsRequiredMembers]
        public TestUpdatumManager() : base("owner", "repository")
        {
        }

        public string? GitHubAssetDigest { get; set; }
        public int GitHubDigestRequestCount { get; private set; }

        internal override Task<string?> GetGitHubAssetDigestAsync(
            ReleaseAsset asset,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GitHubDigestRequestCount++;
            return Task.FromResult(GitHubAssetDigest);
        }
    }

    private sealed class CountingReadStream(byte[] data) : MemoryStream(data)
    {
        public int BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            BytesRead = checked((int)Position);
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = base.Read(buffer.Span);
            BytesRead = checked((int)Position);
            return ValueTask.FromResult(read);
        }
    }
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StubOctokitHttpClient(Func<Octokit.Internal.IRequest, IResponse> responseFactory)
        : Octokit.Internal.IHttpClient
    {
        public Task<IResponse> Send(
            Octokit.Internal.IRequest request,
            CancellationToken cancellationToken,
            Func<object, object> postProcessing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(request));
        }

        public void SetRequestTimeout(TimeSpan timeout)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubOctokitResponse(string body) : IResponse
    {
        public object Body { get; } = body;
        public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public ApiInfo ApiInfo { get; } = new(
            new Dictionary<string, Uri>(),
            [],
            [],
            string.Empty,
            new RateLimit(),
            TimeSpan.Zero);
        public HttpStatusCode StatusCode => HttpStatusCode.OK;
        public string ContentType => "application/json";
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            _callbacks.Enqueue((d, state));
        }

        public void Drain()
        {
            while (_callbacks.TryDequeue(out var work)) work.Callback(work.State);
        }
    }
}
