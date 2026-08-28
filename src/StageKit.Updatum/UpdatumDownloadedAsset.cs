using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Octokit;
using StageKit.Updatum.Extensions;

namespace StageKit.Updatum;

/// <summary>
/// Represents a download of a release asset.
/// </summary>
/// <param name="Release"></param>
/// <param name="ReleaseAsset"></param>
/// <param name="FilePath"></param>
public record UpdatumDownloadedAsset(Release Release, ReleaseAsset ReleaseAsset, string FilePath)
{
    /// <summary>
    /// Gets the SHA-256 digest of the downloaded file when it was verified against a checksum release asset.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Gets a value indicating whether the downloaded file was verified against a SHA-256 checksum release asset.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Sha256))]
    public bool IsChecksumVerified => Sha256 is not null;

    /// <summary>
    /// Gets a value indicating whether the downloaded file passed the configured platform-specific signature verifier.
    /// </summary>
    public bool IsSignatureVerified { get; init; }

    internal string? TemporaryDirectoryPath { get; init; }

    /// <summary>
    /// Gets the release tag version, excluding the v prefix if present.
    /// </summary>
    public string TagVersionStr => Release.GetTagVersionStr();

    /// <summary>
    /// Checks if the downloaded file exists at the <see cref="FilePath"/> path.
    /// </summary>
    public bool FileExists => File.Exists(FilePath);

    /// <summary>
    /// Gets the file name of the downloaded asset.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the file name without extension of the downloaded asset.
    /// </summary>
    public string FileNameNoExt => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// Gets the file extension of the downloaded asset.
    /// </summary>
    public string FileExtension => Path.GetExtension(FilePath);

    /// <summary>
    /// Perform a safe <see cref="FilePath"/> file deletion.
    /// </summary>
    public void SafeDeleteFile()
    {
        try
        {
            if (FileExists) File.Delete(FilePath);

            if (!string.IsNullOrWhiteSpace(TemporaryDirectoryPath)
                && Directory.Exists(TemporaryDirectoryPath)
                && !Directory.EnumerateFileSystemEntries(TemporaryDirectoryPath).Any())
            {
                Directory.Delete(TemporaryDirectoryPath);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
}