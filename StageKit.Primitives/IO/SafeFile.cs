using System.Text;

namespace StageKit.Primitives;

/// <summary>
/// Provides atomic file write helpers that write to a temporary file before replacing the destination.
/// </summary>
public static class SafeFile
{
    /// <summary>
    /// Formats a temporary file path by appending a unique identifier and ".tmp" extension to the specified file path. This can be used to generate a temporary file name for atomic write operations.
    /// </summary>
    /// <param name="filePath">The original file path.</param>
    /// <returns>A temporary file path with a unique identifier and ".tmp" extension.</returns>
    /// <remarks>This method does not do any validation.</remarks>
    public static string FormatTemporaryPathName(string filePath)
    {
        return $"{filePath}.tmp.{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Determines whether a path is a temporary file path generated for the specified destination file path.
    /// </summary>
    /// <param name="path">The path to evaluate.</param>
    /// <param name="destinationFilePath">The destination file path associated with the temporary file.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> uses the temporary-file naming pattern for <paramref name="destinationFilePath"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> or <paramref name="destinationFilePath"/> is null or whitespace.</exception>
    public static bool IsTemporaryPathFor(string path, string destinationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return Path.GetFullPath(path).StartsWith(
            Path.GetFullPath(destinationFilePath) + ".tmp.",
            comparison);
    }

    /// <summary>
    /// Formats a temporary file path by appending a unique identifier and ".tmp" extension to the specified file path. This can be used to generate a temporary file name for atomic write operations.
    /// </summary>
    /// <param name="filePath">The original file path.</param>
    /// <param name="fullPath">The full path of the original file.</param>
    /// <param name="createDirectory">Indicates whether to create the directory if it does not exist.</param>
    /// <returns>A temporary file path with a unique identifier and ".tmp" extension.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    /// <remarks>This method validates the input file path before formatting.</remarks>
    public static string FormatTemporaryPathNameSanitized(string filePath, out string fullPath, bool createDirectory = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        fullPath = Path.GetFullPath(filePath);
        if (createDirectory)
        {
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        return FormatTemporaryPathName(fullPath);
    }

    /// <summary>
    /// Formats a temporary file path by appending a unique identifier and ".tmp" extension to the specified file path. This can be used to generate a temporary file name for atomic write operations.
    /// </summary>
    /// <param name="filePath">The original file path.</param>
    /// <param name="createDirectory">Indicates whether to create the directory if it does not exist.</param>
    /// <returns>A temporary file path with a unique identifier and ".tmp" extension.</returns>
    /// <remarks>This method validates the input file path before formatting.</remarks>
    public static string FormatTemporaryPathNameSanitized(string filePath, bool createDirectory = false)
    {
        return FormatTemporaryPathNameSanitized(filePath, out _, createDirectory);
    }

    /// <summary>
    /// Atomically writes text to the specified file.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="contents">The text contents to write.</param>
    /// <param name="encoding">The text encoding to use. UTF-8 is used when omitted.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    public static void WriteAllText(string filePath, string? contents, Encoding? encoding = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        encoding ??= Encoding.UTF8;
        Write(filePath, stream =>
        {
            using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
            writer.Write(contents);
            writer.Flush();
        });
    }

    /// <summary>
    /// Atomically writes text to the specified file asynchronously.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="contents">The text contents to write.</param>
    /// <param name="encoding">The text encoding to use. UTF-8 is used when omitted.</param>
    /// <param name="cancellationToken">A token that can cancel the write before the destination is replaced.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    public static Task WriteAllTextAsync(
        string filePath,
        string? contents,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        encoding ??= Encoding.UTF8;
        return WriteAsync(
            filePath,
            async (stream, token) =>
            {
                await using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
                if (contents is not null)
                {
                    await writer.WriteAsync(contents.AsMemory(), token).ConfigureAwait(false);
                }

                await writer.FlushAsync(token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Atomically writes bytes to the specified file.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="bytes"/> is null.</exception>
    public static void WriteAllBytes(string filePath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(bytes);

        Write(filePath, stream => stream.Write(bytes));
    }

    /// <summary>
    /// Atomically writes bytes to the specified file asynchronously.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="cancellationToken">A token that can cancel the write before the destination is replaced.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="bytes"/> is null.</exception>
    public static Task WriteAllBytesAsync(
        string filePath,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(bytes);

        return WriteAsync(
            filePath,
            async (stream, token) => await stream.WriteAsync(bytes, token).ConfigureAwait(false),
            cancellationToken);
    }

    /// <summary>
    /// Atomically writes a file by passing a temporary file stream to the supplied writer.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="write">The callback that writes the destination contents to a temporary stream.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="write"/> is null.</exception>
    public static void Write(string filePath, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        var tempPath = FormatTemporaryPathNameSanitized(filePath, out var fullPath, true);
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }

    /// <summary>
    /// Atomically writes a file asynchronously by passing a temporary file stream to the supplied writer.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="write">The callback that writes the destination contents to a temporary stream.</param>
    /// <param name="cancellationToken">A token that can cancel the write before the destination is replaced.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="write"/> is null.</exception>
    public static async Task WriteAsync(
        string filePath,
        Func<Stream, CancellationToken, ValueTask> write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        var tempPath = FormatTemporaryPathNameSanitized(filePath, out var fullPath, true);
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await write(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }
}
