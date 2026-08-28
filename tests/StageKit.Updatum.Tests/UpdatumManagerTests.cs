using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Octokit;

namespace StageKit.Updatum.Tests;

public sealed class UpdatumManagerTests
{
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

    private static UpdatumManager CreateManager(HttpClient? httpClient = null)
    {
        return new UpdatumManager("owner", "repository")
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

    private static Release CreateRelease(IReadOnlyList<ReleaseAsset> assets)
    {
        return new Release(
            "https://api.example.test/release",
            "https://example.test/release",
            "https://api.example.test/assets",
            "https://uploads.example.test/assets",
            1,
            "node",
            "v2.0.0",
            "main",
            "Version 2.0.0",
            "Changes",
            false,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null!,
            "https://example.test/source.tar.gz",
            "https://example.test/source.zip",
            assets);
    }

    private static ReleaseAsset CreateAsset(string name, string url, int size)
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
            url,
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
