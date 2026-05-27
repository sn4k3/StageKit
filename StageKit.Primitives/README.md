# StageKit.Primitives

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/master/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit.Primitives?style=for-the-badge)](https://www.nuget.org/packages/StageKit.Primitives)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit.Primitives is a dependency-light .NET package with reusable low-level helpers used by StageKit libraries and available for other libraries or apps.

All public helpers are exposed from the `StageKit.Primitives` namespace. IO-related source files are grouped under an `IO/` folder for organization only.

## Features

- Atomic file writes with temporary-file replacement through `SafeFile`
- Stream-based atomic file writes through `SafeFileStream`
- Path, temporary file, and temporary directory helpers
- Disposable base type with thread-safe idempotent disposal through `DisposableObject`
- Leave-open lifecycle base type through `LeaveOpenDisposableObject`
- `SafeHandle` wrapper for pinned `GCHandle` scenarios through `GCSafeHandle`

## Install

```bash
dotnet add package StageKit.Primitives
```

## Requirements

- .NET 8 or newer
- C# latest language version

## SafeFile

Use `SafeFile` when you need to write a file through a temporary file and then replace the destination.

```csharp
using StageKit.Primitives;

SafeFile.WriteAllText("settings.json", json);

SafeFile.Write("settings.json", stream =>
{
    JsonSerializer.Serialize(stream, settings);
});
```

Async writes are supported:

```csharp
await SafeFile.WriteAllTextAsync(
    "settings.json",
    json,
    cancellationToken: cancellationToken);
```

Temporary files use this pattern:

```text
<destination>.tmp.<guid>
```

Use `SafeFile.IsTemporaryPathFor(...)` when filtering a directory that may contain temporary files for an in-progress write:

```csharp
if (SafeFile.IsTemporaryPathFor(candidatePath, destinationPath))
{
    return;
}
```

## SafeFileStream

Use `SafeFileStream` when you want stream-style writes with atomic replacement.

```csharp
using StageKit.Primitives;
using System.Text;

using var stream = new SafeFileStream("settings.json");
stream.Write(Encoding.UTF8.GetBytes(json));
// Dispose commits by default.
```

Set `commitOnDispose` to `false` when you want to commit explicitly:

```csharp
await using var stream = new SafeFileStream("settings.json", commitOnDispose: false);
await stream.WriteAsync(buffer, cancellationToken);
await stream.CommitAsync(cancellationToken);
```

If a `SafeFileStream` is disposed without committing and `commitOnDispose` is `false`, the temporary file is deleted and the destination is left unchanged.

## IO Helpers

Use `PathUtilities` for platform-aware path comparisons and archive entry normalization:

```csharp
if (!PathUtilities.IsSubPathOf(candidatePath, rootPath))
{
    throw new InvalidOperationException("Path escapes the root directory.");
}

var entryName = PathUtilities.NormalizeArchiveEntryName(relativePath);
```

Use `TemporaryDirectory` when a temporary workspace should be removed automatically:

```csharp
using var directory = new TemporaryDirectory(prefix: "stagekit");
var outputPath = Path.Combine(directory.DirectoryPath, "output.json");
```

Use `TemporaryFile` when a temporary file should be removed unless explicitly kept:

```csharp
using var file = new TemporaryFile(extension: "json");
await File.WriteAllTextAsync(file.FilePath, json);

file.Keep();
```

## DisposableObject

Use `DisposableObject` for classes that need idempotent deterministic cleanup.

```csharp
using StageKit.Primitives;

public sealed class Worker : DisposableObject
{
    private readonly Stream _stream;

    public Worker(Stream stream)
    {
        _stream = stream;
    }

    public void Run()
    {
        ThrowIfDisposed();

        // Use the stream.
    }

    protected override void DisposeManaged()
    {
        _stream.Dispose();
    }
}
```

`DisposeManaged()` runs for normal `Dispose()` calls. `DisposeUnmanaged()` always runs after managed cleanup is attempted, even if managed cleanup throws. The base class does not define a finalizer; types that directly own unmanaged resources should prefer `SafeHandle` or implement their own finalizer.

## LeaveOpenDisposableObject

Use `LeaveOpenDisposableObject` when a type needs to expose a leave-open option for a resource owned by the caller.

```csharp
using StageKit.Primitives;

public sealed class StreamWriterOwner : LeaveOpenDisposableObject
{
    private readonly Stream _stream;

    public StreamWriterOwner(Stream stream, bool leaveOpen)
        : base(leaveOpen)
    {
        _stream = stream;
    }

    protected override void DisposeManaged()
    {
        if (!LeaveOpen)
        {
            _stream.Dispose();
        }
    }
}
```

Derived classes are responsible for honoring `LeaveOpen`.

## GCSafeHandle

`GCSafeHandle` wraps a `GCHandle` in a `SafeHandle` so pinned memory can be released reliably.

```csharp
using StageKit.Primitives;

var buffer = new byte[1024];
using var handle = new GCSafeHandle(buffer);

IntPtr address = handle.DangerousGetHandle();
```

Use this only when pinning is necessary, such as interop paths that require a stable address. Keep pinning lifetimes short.

## License

StageKit.Primitives is licensed under the MIT License.
