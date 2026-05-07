namespace StageKit;

/// <summary>
/// Reports the result of applying a file retention policy.
/// </summary>
public sealed class FileRetentionResult
{
    private readonly List<string> _deletedFiles = [];
    private readonly List<string> _failedFiles = [];

    /// <summary>
    /// Gets the deleted file paths.
    /// </summary>
    public IReadOnlyList<string> DeletedFiles => _deletedFiles;

    /// <summary>
    /// Gets file paths that could not be deleted.
    /// </summary>
    public IReadOnlyList<string> FailedFiles => _failedFiles;

    /// <summary>
    /// Gets the number of deleted files.
    /// </summary>
    public int DeletedCount => _deletedFiles.Count;

    /// <summary>
    /// Gets the number of files that could not be deleted.
    /// </summary>
    public int FailedCount => _failedFiles.Count;

    internal void AddDeleted(string filePath) => _deletedFiles.Add(filePath);

    internal void AddFailed(string filePath) => _failedFiles.Add(filePath);
}
