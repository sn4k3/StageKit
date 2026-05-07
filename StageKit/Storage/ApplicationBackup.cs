using System.IO.Compression;

namespace StageKit;

/// <summary>
/// Creates and restores zip backups for an application profile directory.
/// </summary>
public static class ApplicationBackup
{
    /// <summary>
    /// Creates a zip backup of the configured application profile directory.
    /// </summary>
    /// <param name="options">Optional backup configuration.</param>
    /// <returns>The created backup file path.</returns>
    public static string Create(ApplicationBackupOptions? options = null)
    {
        options ??= new ApplicationBackupOptions();

        var sourceDirectoryPath = Path.GetFullPath(options.SourceDirectoryPath);
        var destinationFilePath = Path.GetFullPath(options.DestinationFilePath ?? CreateDefaultBackupPath());
        var backupsDirectoryPath = Path.GetFullPath(ApplicationKit.BackupsPath);

        if (!Directory.Exists(sourceDirectoryPath))
        {
            Directory.CreateDirectory(sourceDirectoryPath);
        }

        SafeFile.Write(destinationFilePath, stream =>
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            AddDirectoryToArchive(
                archive,
                sourceDirectoryPath,
                sourceDirectoryPath,
                filePath =>
                {
                    var fullFilePath = Path.GetFullPath(filePath);
                    if (StringComparer.OrdinalIgnoreCase.Equals(fullFilePath, destinationFilePath))
                    {
                        return false;
                    }

                    if (fullFilePath.StartsWith(destinationFilePath + ".tmp-", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return options.IncludeBackupsDirectory
                           || !IsSubPathOf(fullFilePath, backupsDirectoryPath);
                });
        });

        return destinationFilePath;
    }

    /// <summary>
    /// Creates a zip backup of the configured application profile directory asynchronously.
    /// </summary>
    /// <param name="options">Optional backup configuration.</param>
    /// <param name="cancellationToken">A token that can cancel the backup before the destination is replaced.</param>
    /// <returns>The created backup file path.</returns>
    public static async Task<string> CreateAsync(
        ApplicationBackupOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ApplicationBackupOptions();

        var sourceDirectoryPath = Path.GetFullPath(options.SourceDirectoryPath);
        var destinationFilePath = Path.GetFullPath(options.DestinationFilePath ?? CreateDefaultBackupPath());
        var backupsDirectoryPath = Path.GetFullPath(ApplicationKit.BackupsPath);

        if (!Directory.Exists(sourceDirectoryPath))
        {
            Directory.CreateDirectory(sourceDirectoryPath);
        }

        await SafeFile.WriteAsync(
            destinationFilePath,
            async (stream, token) =>
            {
#if NET10_0_OR_GREATER
                await using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
#else
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
#endif
                await AddDirectoryToArchiveAsync(
                    archive,
                    sourceDirectoryPath,
                    sourceDirectoryPath,
                    filePath =>
                    {
                        var fullFilePath = Path.GetFullPath(filePath);
                        if (StringComparer.OrdinalIgnoreCase.Equals(fullFilePath, destinationFilePath))
                        {
                            return false;
                        }

                        if (fullFilePath.StartsWith(destinationFilePath + ".tmp-", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        return options.IncludeBackupsDirectory
                               || !IsSubPathOf(fullFilePath, backupsDirectoryPath);
                    },
                    cancellationToken: token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return destinationFilePath;
    }

    /// <summary>
    /// Restores a zip backup into the configured application profile directory.
    /// </summary>
    /// <param name="backupFilePath">The backup zip file path.</param>
    /// <param name="destinationDirectoryPath">The restore target directory. Defaults to <see cref="ApplicationKit.ProfilePath"/>.</param>
    /// <param name="overwrite">Whether existing files are overwritten.</param>
    public static void Restore(string backupFilePath, string? destinationDirectoryPath = null, bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);

        var destinationRoot = Path.GetFullPath(destinationDirectoryPath ?? ApplicationKit.ProfilePath);
        Directory.CreateDirectory(destinationRoot);

        using var archive = ZipFile.OpenRead(backupFilePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!IsSubPathOf(destinationPath, destinationRoot))
            {
                throw new InvalidOperationException($"Backup entry escapes the destination directory: {entry.FullName}");
            }

            if (!overwrite && File.Exists(destinationPath))
            {
                continue;
            }

            var directoryPath = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using var entryStream = entry.Open();
            SafeFile.Write(destinationPath, entryStream.CopyTo);
        }
    }

    /// <summary>
    /// Restores a zip backup into the configured application profile directory asynchronously.
    /// </summary>
    /// <param name="backupFilePath">The backup zip file path.</param>
    /// <param name="destinationDirectoryPath">The restore target directory. Defaults to <see cref="ApplicationKit.ProfilePath"/>.</param>
    /// <param name="overwrite">Whether existing files are overwritten.</param>
    /// <param name="cancellationToken">A token that can cancel the restore before the current destination file is replaced.</param>
    /// <returns>A task that represents the asynchronous restore operation.</returns>
    public static async Task RestoreAsync(
        string backupFilePath,
        string? destinationDirectoryPath = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);

        var destinationRoot = Path.GetFullPath(destinationDirectoryPath ?? ApplicationKit.ProfilePath);
        Directory.CreateDirectory(destinationRoot);

#if NET10_0_OR_GREATER
        await using var archive = await ZipFile.OpenReadAsync(backupFilePath, cancellationToken);
#else
        using var archive = ZipFile.OpenRead(backupFilePath);
#endif
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!IsSubPathOf(destinationPath, destinationRoot))
            {
                throw new InvalidOperationException($"Backup entry escapes the destination directory: {entry.FullName}");
            }

            if (!overwrite && File.Exists(destinationPath))
            {
                continue;
            }

            var directoryPath = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

#if NET10_0_OR_GREATER
            await using var entryStream = await entry.OpenAsync(cancellationToken);
#else
            using var entryStream = entry.Open();
#endif
            await SafeFile.WriteAsync(
                destinationPath,
                async (stream, token) => await entryStream.CopyToAsync(stream, token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a directory and its subdirectories to a zip archive, preserving relative paths. Optionally filters files and adds an entry prefix.
    /// </summary>
    /// <param name="archive">The zip archive to add the directory to.</param>
    /// <param name="rootDirectoryPath">The root directory path for calculating relative paths.</param>
    /// <param name="directoryPath">The directory path to add to the archive.</param>
    /// <param name="includeFile">An optional function to filter which files to include.</param>
    /// <param name="entryPrefix">An optional prefix to add to each entry in the archive.</param>
    internal static void AddDirectoryToArchive(
        ZipArchive archive,
        string rootDirectoryPath,
        string directoryPath,
        Func<string, bool>? includeFile = null,
        string? entryPrefix = null)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            if (includeFile?.Invoke(filePath) == false)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootDirectoryPath, filePath);
            var entryName = string.IsNullOrWhiteSpace(entryPrefix)
                ? relativePath
                : Path.Combine(entryPrefix, relativePath);
            archive.CreateEntryFromFile(filePath, NormalizeEntryName(entryName), CompressionLevel.Optimal);
        }
    }

    internal static async Task AddDirectoryToArchiveAsync(
        ZipArchive archive,
        string rootDirectoryPath,
        string directoryPath,
        Func<string, bool>? includeFile = null,
        string? entryPrefix = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (includeFile?.Invoke(filePath) == false)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootDirectoryPath, filePath);
            var entryName = string.IsNullOrWhiteSpace(entryPrefix)
                ? relativePath
                : Path.Combine(entryPrefix, relativePath);
            await AddFileToArchiveAsync(
                archive,
                filePath,
                NormalizeEntryName(entryName),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a single file to a zip archive entry by streaming its contents, which is more efficient for large files than using ZipArchive.CreateEntryFromFile. The entry name is normalized to use forward slashes.
    /// </summary>
    /// <param name="archive">The zip archive to add the file to.</param>
    /// <param name="filePath">The path of the file to add.</param>
    /// <param name="entryName">The name of the entry in the zip archive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task AddFileToArchiveAsync(
        ZipArchive archive,
        string filePath,
        string entryName,
        CancellationToken cancellationToken = default)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
#if NET10_0_OR_GREATER
        await using var entryStream = await entry.OpenAsync(cancellationToken);
#else
        using var entryStream = entry.Open();
#endif
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await fileStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether a given path is a subpath of a specified root path, accounting for relative paths and directory traversal. Both paths are normalized to absolute form before comparison.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="rootPath">The root path to compare against.</param>
    /// <returns>True if the path is a subpath of the root path; otherwise, false.</returns>
    internal static bool IsSubPathOf(string path, string rootPath)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a zip archive entry name by replacing directory separators with forward slashes, as required by the zip format.
    /// </summary>
    /// <param name="entryName">The entry name to normalize.</param>
    /// <returns>The normalized entry name.</returns>
    internal static string NormalizeEntryName(string entryName)
    {
        return entryName.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Creates a default backup file path with a timestamped file name under the application backups directory.
    /// </summary>
    /// <returns>The default backup file path.</returns>
    private static string CreateDefaultBackupPath()
    {
        var fileName = $"{ApplicationKit.ApplicationName}-backup-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip";
        return Path.Combine(ApplicationKit.BackupsPath, fileName);
    }
}
