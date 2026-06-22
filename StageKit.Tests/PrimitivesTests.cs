using StageKit.Primitives;
using System.Runtime.InteropServices;
using System.Text;

namespace StageKit.Tests;

public sealed class PrimitivesTests
{
    [Fact]
    public void Dispose_WhenManagedDisposeThrows_StillDisposesUnmanagedResources()
    {
        var disposable = new ThrowingManagedDisposeObject();

        Assert.Throws<InvalidOperationException>(disposable.Dispose);

        Assert.True(disposable.ManagedDisposeCalled);
        Assert.True(disposable.UnmanagedDisposeCalled);
    }

    [Fact]
    public void SafeFileStream_Dispose_WhenCommitOnDisposeTrue_ReplacesDestination()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        File.WriteAllText(filePath, "old");

        using (var stream = new SafeFileStream(filePath))
        {
            stream.Write("new"u8);
            Assert.True(File.Exists(stream.TemporaryPath));
        }

        Assert.Equal("new", File.ReadAllText(filePath));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public void SafeFileStream_Dispose_WhenCommitOnDisposeFalse_DeletesTemporaryFileAndPreservesDestination()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        File.WriteAllText(filePath, "old");

        using (var stream = new SafeFileStream(filePath, commitOnDispose: false))
        {
            stream.Write("new"u8);
            Assert.True(File.Exists(stream.TemporaryPath));
        }

        Assert.Equal("old", File.ReadAllText(filePath));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public async Task SafeFileStream_CommitAsync_ReplacesDestination()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        await File.WriteAllTextAsync(filePath, "old", TestContext.Current.CancellationToken);

        await using (var stream = new SafeFileStream(filePath, commitOnDispose: false))
        {
            await stream.WriteAsync("new"u8.ToArray(), TestContext.Current.CancellationToken);
            await stream.CommitAsync(TestContext.Current.CancellationToken);
            Assert.True(stream.IsCommitted);
        }

        Assert.Equal("new", await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public void TemporaryDirectory_Dispose_DeletesDirectoryRecursively()
    {
        string directoryPath;

        using (var directory = new TemporaryDirectory(CreateTempDirectory(), "stagekit"))
        {
            directoryPath = directory.DirectoryPath;
            Directory.CreateDirectory(Path.Combine(directoryPath, "child"));
            File.WriteAllText(Path.Combine(directoryPath, "child", "file.txt"), "value");

            Assert.True(directory.Exists);
        }

        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public void TemporaryFile_Dispose_DeletesFileUnlessKept()
    {
        var directoryPath = CreateTempDirectory();
        string deletedPath;
        string keptPath;

        using (var temporaryFile = new TemporaryFile(directoryPath, "txt"))
        {
            deletedPath = temporaryFile.FilePath;
            using var stream = temporaryFile.Create();
            stream.Write("deleted"u8);
        }

        using (var temporaryFile = new TemporaryFile(directoryPath, ".txt"))
        {
            keptPath = temporaryFile.FilePath;
            using var stream = temporaryFile.Create();
            stream.Write("kept"u8);
            temporaryFile.Keep();
        }

        Assert.False(File.Exists(deletedPath));
        Assert.True(File.Exists(keptPath));
    }

    [Fact]
    public void PathUtilities_IsSubPathOf_RequiresDirectoryBoundary()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", "root");
        var childPath = Path.Combine(rootPath, "child", "file.txt");
        var siblingWithPrefixPath = rootPath + "-other";

        Assert.True(PathUtilities.IsSubPathOf(childPath, rootPath));
        Assert.False(PathUtilities.IsSubPathOf(siblingWithPrefixPath, rootPath));
    }

    [Fact]
    public void UnmanagedMemoryManager_GetSpan_ReflectsUnderlyingMemory()
    {
        var pointer = Marshal.AllocHGlobal(sizeof(int) * 3);

        try
        {
            Marshal.WriteInt32(pointer, 0, 10);
            Marshal.WriteInt32(pointer, sizeof(int), 20);
            Marshal.WriteInt32(pointer, sizeof(int) * 2, 30);

            using var manager = new UnmanagedMemoryManager<int>(pointer, 3);
            var span = manager.GetSpan();

            Assert.Equal([10, 20, 30], span.ToArray());

            span[1] = 42;

            Assert.Equal(42, Marshal.ReadInt32(pointer, sizeof(int)));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void UnmanagedMemoryManager_AllowsNullPointerForEmptyBlock()
    {
        using var manager = new UnmanagedMemoryManager<byte>(nint.Zero, 0);

        Assert.True(manager.GetSpan().IsEmpty);

        using var handle = manager.Pin();
    }

    [Fact]
    public void UnmanagedMemoryManager_Constructor_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnmanagedMemoryManager<byte>(nint.Zero, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnmanagedMemoryManager<byte>(1, -1));
    }

    [Fact]
    public void UnmanagedMemoryManager_Pin_ValidatesElementIndex()
    {
        var pointer = Marshal.AllocHGlobal(1);

        try
        {
            using var manager = new UnmanagedMemoryManager<byte>(pointer, 1);

            using var endHandle = manager.Pin(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Pin(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Pin(2));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private sealed class ThrowingManagedDisposeObject : DisposableObject
    {
        public bool ManagedDisposeCalled { get; private set; }

        public bool UnmanagedDisposeCalled { get; private set; }

        protected override void DisposeManaged()
        {
            ManagedDisposeCalled = true;
            throw new InvalidOperationException();
        }

        protected override void DisposeUnmanaged()
        {
            UnmanagedDisposeCalled = true;
        }
    }
}
