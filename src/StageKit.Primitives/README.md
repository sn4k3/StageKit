# StageKit.Primitives

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/main/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit.Primitives?style=for-the-badge)](https://www.nuget.org/packages/StageKit.Primitives)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit.Primitives is a dependency-light .NET package with reusable low-level helpers used by StageKit libraries and
available for other libraries or apps.

All public helpers are exposed from the `StageKit.Primitives` namespace. IO-related source files are grouped under an
`IO/` folder for organization only.

## Features

- Atomic file writes with temporary-file replacement through `SafeFile`
- Stream-based atomic file writes through `SafeFileStream`
- Path, leaf-name validation, temporary file, and temporary directory helpers
- Bash ANSI-C and Windows batch value quoting helpers through `StringExtensions`
- Host-aware path comparison and Unix executable-permission helpers
- Disposable base type with thread-safe idempotent disposal through `DisposableObject`
- Finalizable disposable base type through `UnmanagedDisposableObject`
- Leave-open lifecycle base type through `LeaveOpenDisposableObject`
- `SafeHandle` wrapper for pinned `GCHandle` scenarios through `GCSafeHandle`
- `MemoryManager<T>` wrapper for externally owned unmanaged buffers through `UnmanagedMemoryManager<T>`

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

Use `SafeFile.IsTemporaryPathFor(...)` when filtering a directory that may contain temporary files for an in-progress
write:

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

If a `SafeFileStream` is disposed without committing and `commitOnDispose` is `false`, the temporary file is deleted and
the destination is left unchanged.

## IO Helpers

Use `PathUtilities` for platform-aware path comparisons and archive entry normalization:

```csharp
if (!PathUtilities.IsSubPathOf(candidatePath, rootPath))
{
    throw new InvalidOperationException("Path escapes the root directory.");
}

var entryName = PathUtilities.NormalizeArchiveEntryName(relativePath);
```

Use `FileUtilities` when a value must be one simple file or directory name rather than a rooted or nested path:

```csharp
if (!FileUtilities.IsPathLeafName(assetName))
{
    throw new InvalidOperationException("The asset name is invalid.");
}

var validatedName = FileUtilities.ValidatePathLeafName(assetName, nameof(assetName));
```

`ValidatePathLeafName(...)` returns the validated value and throws `InvalidOperationException` for blank names, rooted
paths, path separators, `.`/`..`, or characters rejected by `Path.GetInvalidFileNameChars()`.

Use `StringExtensions` when generating shell scripts:

```csharp
var bashValue = value.QuoteBashAnsiCString();
var batchValue = value.EscapeWindowsBatchValue();
```

`QuoteBashAnsiCString()` rejects null characters because Bash variables cannot contain them. The batch helper removes
carriage returns, writes line feeds as `\n`, and escapes batch metacharacters for delayed expansion.

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

## System Helpers

`HostSystem.HostStringComparison` provides the comparison StageKit uses for file-system paths: ordinal,
case-insensitive comparison on Windows and ordinal comparison elsewhere.

```csharp
using StageKit.Primitives.System;

bool samePath = string.Equals(leftPath, rightPath, HostSystem.HostStringComparison);
```

Use `HostSystem.TryFindExecutable(...)` to resolve an executable without starting a lookup process. It honors `PATHEXT`
on Windows and requires an execute permission bit on Unix:

```csharp
if (HostSystem.TryFindExecutable("git", out string? gitPath))
    Console.WriteLine(gitPath);
```

Use `UnixSystem.SetUnix755Executable(...)` to grant owner write/execute and group/other execute permissions to a Unix
launcher. The method is a no-op on Windows.

```csharp
UnixSystem.SetUnix755Executable(scriptPath);
```

Use `ProcessHelper.StartProcess(...)` to launch a command, optionally waiting for its exit code. Set
`requireElevation: true` to show the platform administrator prompt through Windows `runas`, Linux `pkexec`, or macOS
`osascript`. Elevation is skipped when `Environment.IsPrivilegedProcess` is already `true`:

```csharp
int exitCode = ProcessHelper.StartProcess(
    "system-tool",
    ["--configure", "value with spaces"],
    requireElevation: true,
    waitForCompletion: true);
```

Run shell syntax through `cmd /c` on Windows or `bash -c` elsewhere, and use the output helpers when the exit code and
both redirected streams are needed. Output helpers also accept `requireElevation`; Linux and macOS capture through their
elevation wrappers, while non-privileged Windows `runas` capture returns exit code `-1` because that API cannot redirect
the elevated child streams:

```csharp
int shellExitCode = ProcessHelper.StartShell("system-tool --configure", waitForCompletion: true);

ProcessOutput output = ProcessHelper.GetShellOutput("system-tool --status", requireElevation: true);
Console.Write(output.StandardOutput);
Console.Error.Write(output.StandardError);

ProcessOutput asyncOutput = await ProcessHelper.GetShellOutputAsync(
    "system-tool --status",
    cancellationToken: cancellationToken);
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

`DisposeManaged()` runs for normal `Dispose()` calls. During explicit disposal, `DisposeUnmanaged()` runs after managed
cleanup is attempted, even if managed cleanup throws. The base class does not define a finalizer; types that directly
own unmanaged resources should prefer `SafeHandle` or use `UnmanagedDisposableObject`.

`UnmanagedDisposableObject` is available when a type directly owns unmanaged resources and needs finalizer fallback.
Prefer `SafeHandle` when the resource is a native handle.

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

Use this only when pinning is necessary, such as interop paths that require a stable address. Keep pinning lifetimes
short.

## UnmanagedMemoryManager

Use `UnmanagedMemoryManager<T>` when an externally owned unmanaged buffer needs to be exposed as `Memory<T>`.

```csharp
using StageKit.Primitives;

using var manager = new UnmanagedMemoryManager<byte>(bufferAddress, bufferLength);
Memory<byte> memory = manager.Memory;
```

The manager does not allocate, pin, or free the underlying memory. The caller must keep the buffer alive and fixed for
every `Memory<T>` or `MemoryHandle` produced by the manager.

## License

StageKit.Primitives is licensed under the MIT License.
