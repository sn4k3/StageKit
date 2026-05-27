namespace StageKit.Primitives;

/// <summary>
/// Provides a writable stream that writes to a temporary file and atomically replaces the destination when committed.
/// </summary>
public sealed class SafeFileStream : Stream
{
    #region Members

    /// <summary>
    /// The underlying file stream writing to the temporary file.
    /// </summary>
    private readonly FileStream _stream;

    /// <summary>
    /// Indicates whether the object has been disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="SafeFileStream"/> class.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="commitOnDispose">Whether disposing the stream should commit the temporary file to the destination.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    public SafeFileStream(string filePath, bool commitOnDispose = true)
    {
        TemporaryPath = SafeFile.FormatTemporaryPathNameSanitized(
            filePath,
            out var destinationPath,
            createDirectory: true);
        DestinationPath = destinationPath;
        CommitOnDispose = commitOnDispose;

        _stream = new FileStream(
            TemporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the destination file path that will be replaced when the stream is committed.
    /// </summary>
    public string DestinationPath { get; }

    /// <summary>
    /// Gets the temporary file path currently being written.
    /// </summary>
    public string TemporaryPath { get; }

    /// <summary>
    /// Gets a value indicating whether disposing the stream commits the temporary file to the destination.
    /// </summary>
    public bool CommitOnDispose { get; }

    /// <summary>
    /// Gets a value indicating whether the temporary file has been committed to the destination.
    /// </summary>
    public bool IsCommitted { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => !_isDisposed && !IsCommitted && _stream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => !_isDisposed && !IsCommitted && _stream.CanWrite;

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            ThrowIfUnavailable();
            return _stream.Length;
        }
    }

    /// <inheritdoc />
    public override long Position
    {
        get
        {
            ThrowIfUnavailable();
            return _stream.Position;
        }
        set
        {
            ThrowIfUnavailable();
            _stream.Position = value;
        }
    }
    #endregion

    #region Methods

    /// <summary>
    /// Flushes the temporary file and atomically replaces the destination file.
    /// </summary>
    public void Commit()
    {
        if (IsCommitted) return;
        ThrowIfDisposed();

        try
        {
            try
            {
                _stream.Flush(flushToDisk: true);
            }
            finally
            {
                _stream.Dispose();
            }

            File.Move(TemporaryPath, DestinationPath, overwrite: true);
            IsCommitted = true;
        }
        catch
        {
            TryDeleteTemporaryFile();
            throw;
        }
        finally
        {
            _isDisposed = true;
        }
    }

    /// <summary>
    /// Flushes the temporary file and atomically replaces the destination file.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the operation before the destination is replaced.</param>
    /// <returns>A task that represents the asynchronous commit operation.</returns>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (IsCommitted) return;
        ThrowIfDisposed();

        try
        {
            try
            {
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(TemporaryPath, DestinationPath, overwrite: true);
            IsCommitted = true;
        }
        catch
        {
            TryDeleteTemporaryFile();
            throw;
        }
        finally
        {
            _isDisposed = true;
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        ThrowIfUnavailable();
        _stream.Flush();
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return _stream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException($"{nameof(SafeFileStream)} is write-only.");
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        throw new NotSupportedException($"{nameof(SafeFileStream)} is write-only.");
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfUnavailable();
        return _stream.Seek(offset, origin);
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        ThrowIfUnavailable();
        _stream.SetLength(value);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfUnavailable();
        _stream.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfUnavailable();
        _stream.Write(buffer);
    }

    /// <inheritdoc />
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return _stream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return _stream.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            if (CommitOnDispose && !IsCommitted)
            {
                Commit();
            }
            else
            {
                _stream.Dispose();
                if (!IsCommitted)
                {
                    TryDeleteTemporaryFile();
                }

                _isDisposed = true;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            await base.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (CommitOnDispose && !IsCommitted)
        {
            await CommitAsync().ConfigureAwait(false);
        }
        else
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            if (!IsCommitted)
            {
                TryDeleteTemporaryFile();
            }

            _isDisposed = true;
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void ThrowIfUnavailable()
    {
        ThrowIfDisposed();
        if (IsCommitted)
        {
            throw new ObjectDisposedException(nameof(SafeFileStream));
        }
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            if (File.Exists(TemporaryPath))
            {
                File.Delete(TemporaryPath);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    #endregion

}
