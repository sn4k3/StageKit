namespace StageKit.Primitives;

/// <summary>
/// Provides a temporary file path and deletes the file when disposed unless it is kept.
/// </summary>
public sealed class TemporaryFile : DisposableObject
{
    private bool _keepFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryFile"/> class.
    /// </summary>
    /// <param name="directoryPath">The directory path. Uses the system temporary directory when omitted.</param>
    /// <param name="extension">The optional file extension.</param>
    public TemporaryFile(string? directoryPath = null, string? extension = null)
    {
        directoryPath ??= Path.GetTempPath();
        Directory.CreateDirectory(directoryPath);

        if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        FilePath = Path.Combine(directoryPath, $"{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// Gets the temporary file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the temporary file exists.
    /// </summary>
    public bool Exists => File.Exists(FilePath);

    /// <summary>
    /// Creates the temporary file and returns a read/write stream.
    /// </summary>
    /// <returns>The created temporary file stream.</returns>
    public FileStream Create()
    {
        ThrowIfDisposed();

        return new FileStream(
            FilePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);
    }

    /// <summary>
    /// Keeps the temporary file when this instance is disposed.
    /// </summary>
    public void Keep()
    {
        ThrowIfDisposed();
        _keepFile = true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return FilePath;
    }

    /// <inheritdoc />
    protected override void DisposeManaged()
    {
        if (_keepFile) return;

        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }
}
