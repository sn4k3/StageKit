using System.Text;

namespace StageKit;

/// <summary>
/// Provides atomic file write helpers that write to a temporary file before replacing the destination.
/// </summary>
public static class SafeFile
{
    /// <summary>
    /// Atomically writes text to the specified file.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="contents">The text contents to write.</param>
    /// <param name="encoding">The text encoding to use. UTF-8 is used when omitted.</param>
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
    public static void Write(string filePath, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(write);

        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempPath = $"{fullPath}.tmp-{Guid.NewGuid():N}";
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
    public static async Task WriteAsync(
        string filePath,
        Func<Stream, CancellationToken, ValueTask> write,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(write);

        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempPath = $"{fullPath}.tmp-{Guid.NewGuid():N}";
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
