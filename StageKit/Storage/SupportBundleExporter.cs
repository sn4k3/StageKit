using StageKit.Primitives;
using StageKit.Runtime;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace StageKit;

/// <summary>
/// Exports a zip support bundle containing diagnostics, selected app files, and a manifest.
/// </summary>
public static class SupportBundleExporter
{
    /// <summary>
    /// Exports a support bundle.
    /// </summary>
    /// <param name="options">Optional export configuration.</param>
    /// <returns>The created support bundle file path.</returns>
    public static string Export(SupportBundleOptions? options = null)
    {
        options ??= new SupportBundleOptions();

        var destinationFilePath = Path.GetFullPath(options.DestinationFilePath ?? CreateDefaultBundlePath());
        SafeFile.Write(destinationFilePath, stream =>
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            AddManifest(archive, options);
            var includeFile = CreateBundleFileFilter(destinationFilePath);

            if (options.IncludeConfigs)
            {
                ApplicationBackup.AddDirectoryToArchive(
                    archive,
                    ApplicationKit.ConfigsPath,
                    ApplicationKit.ConfigsPath,
                    includeFile,
                    entryPrefix: "configs");
            }

            if (options.IncludeLogs)
            {
                ApplicationBackup.AddDirectoryToArchive(
                    archive,
                    ApplicationKit.LogsPath,
                    ApplicationKit.LogsPath,
                    includeFile,
                    entryPrefix: "logs");
            }
            else if (options.IncludeCrashReports)
            {
                AddFileIfExists(
                    archive,
                    Path.Combine(ApplicationKit.LogsPath, CrashReportsFile.CrashReportsFileName),
                    $"crash-reports/{CrashReportsFile.CrashReportsFileName}");
            }

            foreach (var additionalFilePath in options.AdditionalFilePaths)
            {
                AddFileIfExists(archive, additionalFilePath, $"additional/{Path.GetFileName(additionalFilePath)}");
            }
        });

        return destinationFilePath;
    }

    /// <summary>
    /// Exports a support bundle asynchronously.
    /// </summary>
    /// <param name="options">Optional export configuration.</param>
    /// <param name="cancellationToken">A token that can cancel the export before the destination is replaced.</param>
    /// <returns>The created support bundle file path.</returns>
    public static async Task<string> ExportAsync(
        SupportBundleOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SupportBundleOptions();

        var destinationFilePath = Path.GetFullPath(options.DestinationFilePath ?? CreateDefaultBundlePath());
        await SafeFile.WriteAsync(
            destinationFilePath,
            async (stream, token) =>
            {
#if NET10_0_OR_GREATER
                await using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
#else
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
#endif
                await AddManifestAsync(archive, options, token).ConfigureAwait(false);
                var includeFile = CreateBundleFileFilter(destinationFilePath);

                if (options.IncludeConfigs)
                {
                    await ApplicationBackup.AddDirectoryToArchiveAsync(
                        archive,
                        ApplicationKit.ConfigsPath,
                        ApplicationKit.ConfigsPath,
                        includeFile,
                        entryPrefix: "configs",
                        cancellationToken: token).ConfigureAwait(false);
                }

                if (options.IncludeLogs)
                {
                    await ApplicationBackup.AddDirectoryToArchiveAsync(
                        archive,
                        ApplicationKit.LogsPath,
                        ApplicationKit.LogsPath,
                        includeFile,
                        entryPrefix: "logs",
                        cancellationToken: token).ConfigureAwait(false);
                }
                else if (options.IncludeCrashReports)
                {
                    await AddFileIfExistsAsync(
                        archive,
                        Path.Combine(ApplicationKit.LogsPath, CrashReportsFile.CrashReportsFileName),
                        $"crash-reports/{CrashReportsFile.CrashReportsFileName}",
                        token).ConfigureAwait(false);
                }

                foreach (var additionalFilePath in options.AdditionalFilePaths)
                {
                    token.ThrowIfCancellationRequested();
                    await AddFileIfExistsAsync(
                        archive,
                        additionalFilePath,
                        $"additional/{Path.GetFileName(additionalFilePath)}",
                        token).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return destinationFilePath;
    }

    /// <summary>
    /// Adds a manifest entry to the support bundle archive containing metadata about the application and export options.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="options"></param>
    private static void AddManifest(ZipArchive archive, SupportBundleOptions options)
    {
        var entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, CreateManifest(options), ApplicationKit.JsonSerializerOptions);
    }

    /// <summary>
    /// Asynchronously adds a manifest file in JSON format to the specified ZIP archive using the provided support
    /// bundle options.
    /// </summary>
    /// <remarks>The manifest is added as a file named "manifest.json" at the root of the archive. If the
    /// operation is canceled via the cancellation token, the manifest may not be added.</remarks>
    /// <param name="archive">The ZIP archive to which the manifest file will be added. Must not be null.</param>
    /// <param name="options">The options used to generate the contents of the manifest file. Must not be null.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task AddManifestAsync(
        ZipArchive archive,
        SupportBundleOptions options,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
#if NET10_0_OR_GREATER
        await using var entryStream = await entry.OpenAsync(cancellationToken);
#else
        using var entryStream = entry.Open();
#endif
        await JsonSerializer.SerializeAsync(
            entryStream,
            CreateManifest(options),
            ApplicationKit.JsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new support bundle manifest populated with application, environment, and user-specified options.
    /// </summary>
    /// <remarks>The returned manifest includes details such as application name, version, operating system
    /// information, and user-specified inclusion flags. Use this method to generate a manifest that accurately reflects
    /// the current environment and the user's selection of bundle contents.</remarks>
    /// <param name="options">The options that specify which components to include in the support bundle manifest, such as configuration
    /// files, logs, crash reports, and additional notes. Cannot be null.</param>
    /// <returns>A populated SupportBundleManifest instance containing metadata about the application, environment, and selected
    /// bundle options.</returns>
    private static SupportBundleManifest CreateManifest(SupportBundleOptions options)
    {
        return new SupportBundleManifest
        {
            ApplicationName = ApplicationKit.ApplicationName,
            Version = EntryApplication.AssemblyVersionString,
            CreatedUtc = DateTimeOffset.UtcNow,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture,
            ProfilePath = ApplicationKit.ProfilePath,
            ConfigsPath = ApplicationKit.ConfigsPath,
            LogsPath = ApplicationKit.LogsPath,
            IncludeConfigs = options.IncludeConfigs,
            IncludeLogs = options.IncludeLogs,
            IncludeCrashReports = options.IncludeCrashReports,
            Notes = options.Notes,
        };
    }

    /// <summary>
    /// Adds a file to the specified zip archive as a new entry if the file exists at the given path.
    /// </summary>
    /// <remarks>If the file does not exist at the specified path, no entry is added to the archive. The entry
    /// name is normalized before being added.</remarks>
    /// <param name="archive">The zip archive to which the file will be added as a new entry.</param>
    /// <param name="filePath">The full path of the file to add to the archive. The file is only added if it exists.</param>
    /// <param name="entryName">The name to use for the new entry within the archive.</param>
    private static void AddFileIfExists(ZipArchive archive, string filePath, string entryName)
    {
        if (!File.Exists(filePath)) return;

        archive.CreateEntryFromFile(
            filePath,
            ApplicationBackup.NormalizeEntryName(entryName),
            CompressionLevel.Optimal);
    }

    /// <summary>
    /// Adds a file to the specified zip archive if the file exists, using the provided entry name.
    /// </summary>
    /// <param name="archive">The zip archive to which the file will be added.</param>
    /// <param name="filePath">The full path of the file to add to the archive. The file is only added if it exists.</param>
    /// <param name="entryName">The name to use for the file entry within the archive.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the file has been added to the
    /// archive, or immediately if the file does not exist.</returns>
    private static async Task AddFileIfExistsAsync(
        ZipArchive archive,
        string filePath,
        string entryName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) return;

        await ApplicationBackup.AddFileToArchiveAsync(
            archive,
            filePath,
            ApplicationBackup.NormalizeEntryName(entryName),
            cancellationToken).ConfigureAwait(false);
    }

    private static Func<string, bool> CreateBundleFileFilter(string destinationFilePath)
    {
        return filePath =>
        {
            var fullFilePath = Path.GetFullPath(filePath);
            if (StringComparer.OrdinalIgnoreCase.Equals(fullFilePath, destinationFilePath))
            {
                return false;
            }

            return !SafeFile.IsTemporaryPathFor(fullFilePath, destinationFilePath);
        };
    }

    private static string CreateDefaultBundlePath()
    {
        var fileName = $"{ApplicationKit.ApplicationName}-support-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip";
        return Path.Combine(ApplicationKit.BackupsPath, fileName);
    }

    /// <summary>
    /// Represents metadata describing the contents and context of a generated support bundle, including application,
    /// environment, and included data details.
    /// </summary>
    /// <remarks>This class is used to capture information about the environment and data included when
    /// creating a support bundle for diagnostics or troubleshooting. It contains properties for application identity,
    /// operating system and process details, user context, and paths to relevant data such as configuration files and
    /// logs. The inclusion flags indicate which types of data are present in the bundle. This class is intended for
    /// internal use when assembling or processing support bundles.</remarks>
    private sealed class SupportBundleManifest
    {
        public string ApplicationName { get; set; } = string.Empty;

        public string? Version { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public string MachineName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string OSDescription { get; set; } = string.Empty;

        public Architecture OSArchitecture { get; set; }

        public string FrameworkDescription { get; set; } = string.Empty;

        public Architecture ProcessArchitecture { get; set; }

        public string ProfilePath { get; set; } = string.Empty;

        public string ConfigsPath { get; set; } = string.Empty;

        public string LogsPath { get; set; } = string.Empty;

        public bool IncludeConfigs { get; set; }

        public bool IncludeLogs { get; set; }

        public bool IncludeCrashReports { get; set; }

        public string? Notes { get; set; }
    }
}
