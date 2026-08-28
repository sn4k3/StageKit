namespace StageKit.Primitives;

/// <summary>
/// Creates a temporary directory and deletes it when disposed.
/// </summary>
public sealed class TemporaryDirectory : DisposableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
    /// </summary>
    /// <param name="parentDirectoryPath">The parent directory path. Uses the system temporary directory when omitted.</param>
    /// <param name="prefix">The directory name prefix. Uses "tmp" when omitted.</param>
    public TemporaryDirectory(string? parentDirectoryPath = null, string? prefix = null)
    {
        parentDirectoryPath ??= Path.GetTempPath();
        prefix = string.IsNullOrWhiteSpace(prefix) ? "tmp" : prefix;

        DirectoryPath = Path.Combine(
            parentDirectoryPath,
            $"{prefix}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>
    /// Gets the temporary directory path.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Gets a value indicating whether the temporary directory exists.
    /// </summary>
    public bool Exists => Directory.Exists(DirectoryPath);

    /// <inheritdoc />
    public override string ToString()
    {
        return DirectoryPath;
    }

    /// <inheritdoc />
    protected override void DisposeManaged()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }
}
