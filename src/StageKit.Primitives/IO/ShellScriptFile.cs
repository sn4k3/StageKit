using System.Diagnostics;
using System.Text;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;

namespace StageKit.Primitives;

/// <summary>
/// Writes a Windows batch or Unix shell script through a <see cref="TextWriter"/> and executes it with the host shell.
/// </summary>
/// <remarks>
/// <p>
/// Script content is buffered in memory and written atomically to <see cref="FilePath"/> by <see cref="Flush()"/>, by
/// any <c>Execute</c> overload, and by disposal when content is still pending and the file is not being deleted.
/// </p>
/// <p>
/// Use <see cref="CreateTemporary(string?, bool)"/> for a throwaway script: it assigns a unique path with the
/// platform script extension and enables <see cref="DeleteOnDispose"/>. Pass an explicit path to the constructor to
/// keep the script on disk.
/// </p>
/// <p>
/// When elevation is requested from a non-elevated Windows process, the script runs through <c>runas</c> and the
/// returned standard output and error are empty because Windows does not support stream redirection for that launch.
/// </p>
/// </remarks>
public sealed class ShellScriptFile : TextWriter
{
    #region Fields

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private readonly StringBuilder _builder = new();
    private bool _isDisposed;
    private bool _isFlushPending;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellScriptFile"/> class with a unique temporary path and the platform script extension.
    /// </summary>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation when executing the script.
    /// </param>
    /// <param name="deleteOnDispose"><see langword="true"/> to delete the script file when this instance is disposed.</param>
    /// <exception cref="ArgumentException">
    /// </exception>
    /// <remarks>The file is kept on disposal. Set <see cref="DeleteOnDispose"/> to change that.</remarks>
    public ShellScriptFile(bool requireElevation = false, bool deleteOnDispose = false) : this(
        TemporaryFile.GetTempFilePath(extension: ScriptFileExtension), requireElevation, deleteOnDispose)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellScriptFile"/> class for the specified script path.
    /// </summary>
    /// <param name="filePath">The script file path. Missing directories are created when the script is written.</param>
    /// <param name="requireElevation">
    /// <see langword="true"/> to request administrator elevation when executing the script.
    /// </param>
    /// <param name="deleteOnDispose"><see langword="true"/> to delete the script file when this instance is disposed.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> is <see langword="null"/>, empty, or white space.
    /// </exception>
    /// <remarks>The file is kept on disposal. Set <see cref="DeleteOnDispose"/> to change that.</remarks>
    public ShellScriptFile(string filePath, bool requireElevation = false, bool deleteOnDispose = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        NewLine = "\n";
        FilePath = Path.GetFullPath(filePath);
        RequireElevation = requireElevation;
        DeleteOnDispose = deleteOnDispose;
        AppendPlatformPreamble();
    }

    /// <summary>
    /// Creates a script that uses a unique temporary path and is deleted when disposed.
    /// </summary>
    /// <param name="directoryPath">The directory path. Uses the system temporary directory when omitted.</param>
    /// <param name="requireElevation"> <see langword="true"/> to request administrator elevation when executing the script.</param>
    /// <returns>A script with <see cref="DeleteOnDispose"/> enabled.</returns>
    public static ShellScriptFile CreateTemporary(string? directoryPath = null, bool requireElevation = false)
    {
        return new ShellScriptFile(TemporaryFile.GetTempFilePath(directoryPath, ScriptFileExtension), requireElevation, true);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the script file extension for the current platform.
    /// </summary>
    /// <value><c>.bat</c> on Windows or <c>.sh</c> on other operating systems.</value>
    public static string ScriptFileExtension => OperatingSystem.IsWindows() ? ".bat" : ".sh";
    
    /// <summary>
    /// Gets the directory path of the script file.
    /// </summary>
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>
    /// Gets the full path of the script file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets or sets a value indicating whether executing the script requests administrator elevation.
    /// </summary>
    public bool RequireElevation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the script file is deleted when this instance is disposed.
    /// </summary>
    /// <remarks>
    /// Enabled by <see cref="CreateTemporary(string?, bool)"/> and disabled for a script constructed with an explicit
    /// path. Deletion is best effort and never throws.
    /// </remarks>
    public bool DeleteOnDispose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the script file is marked executable after it is written.
    /// </summary>
    /// <remarks>
    /// Unix mode <c>755</c> is applied on a best-effort basis. Windows has no mode bits, so the setting is ignored
    /// there. Execution through this class does not need the bit because the script is passed to the shell, but a
    /// script that is kept on disk usually should carry it.
    /// </remarks>
    public bool SetExecutablePermission { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether the script file exists.
    /// </summary>
    public bool Exists => File.Exists(FilePath);

    /// <summary>
    /// Gets a value indicating whether the buffered script differs from the last content written to disk.
    /// </summary>
    public bool IsFlushPending => _isFlushPending;

    /// <summary>
    /// Gets a value indicating whether this instance has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <inheritdoc />
    /// <remarks>UTF-8 without a byte-order mark, which every supported shell reads correctly.</remarks>
    public override Encoding Encoding => Utf8WithoutBom;

    #endregion

    #region Script content

    /// <summary>
    /// Gets the buffered script content.
    /// </summary>
    /// <returns>The script text, including the platform preamble.</returns>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public string GetScript()
    {
        ThrowIfDisposed();
        return _builder.ToString();
    }

    /// <summary>
    /// Removes all buffered script content and restores the platform preamble.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <remarks>The script file is not rewritten until the next flush or execution.</remarks>
    public void Clear()
    {
        ThrowIfDisposed();
        _builder.Clear();
        AppendPlatformPreamble();
        _isFlushPending = true;
    }

    /// <summary>
    /// Keeps the script file when this instance is disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Keep()
    {
        ThrowIfDisposed();
        DeleteOnDispose = false;
    }

    #endregion

    #region Write overrides

    /// <inheritdoc />
    public override void Write(char value)
    {
        ThrowIfDisposed();
        _builder.Append(value);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        ThrowIfDisposed();
        if (value is null) return;

        _builder.Append(value);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        _builder.Append(buffer, index, count);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<char> buffer)
    {
        ThrowIfDisposed();
        _builder.Append(buffer);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void Write(StringBuilder? value)
    {
        ThrowIfDisposed();
        if (value is null) return;

        _builder.Append(value);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void WriteLine()
    {
        ThrowIfDisposed();
        _builder.Append(CoreNewLine);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        ThrowIfDisposed();
        _builder.Append(value).Append(CoreNewLine);
        _isFlushPending = true;
    }

    /// <inheritdoc />
    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        ThrowIfDisposed();
        _builder.Append(buffer).Append(CoreNewLine);
        _isFlushPending = true;
    }

    /// <summary>
    /// Appends each line followed by a line terminator.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteLines(params string?[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        WriteLines((IEnumerable<string?>)lines);
    }

    /// <summary>
    /// Appends each line of a sequence followed by a line terminator.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteLines(IEnumerable<string?> lines)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines)
        {
            _builder.Append(line).Append(CoreNewLine);
            _isFlushPending = true;
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(char value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(string? value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(char[] buffer, int index, int count)
    {
        Write(buffer, index, count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

        Write(buffer.Span);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(char value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(char[] buffer, int index, int count)
    {
        WriteLine(buffer, index, count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

        WriteLine(buffer.Span);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteLineAsync()
    {
        WriteLine();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Appends each line followed by a line terminator.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <returns>A completed task; the script is buffered in memory until it is flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public Task WriteLinesAsync(params string?[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return WriteLinesAsync((IEnumerable<string?>)lines);
    }

    /// <summary>
    /// Appends each line of a sequence followed by a line terminator.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <returns>A completed task; the script is buffered in memory until it is flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public Task WriteLinesAsync(IEnumerable<string?> lines)
    {
        WriteLines(lines);
        return Task.CompletedTask;
    }

    #endregion

    #region Conditional writes

    /// <summary>
    /// Appends text when <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition that controls whether the text is appended.</param>
    /// <param name="text">The text to append.</param>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteIf(bool condition, string? text)
    {
        ThrowIfDisposed();
        if (condition) Write(text);
    }

    /// <summary>
    /// Appends text followed by a line terminator when <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition that controls whether the line is appended.</param>
    /// <param name="text">The text to append.</param>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteLineIf(bool condition, string? text)
    {
        ThrowIfDisposed();
        if (condition) WriteLine(text);
    }

    /// <summary>
    /// Appends each line followed by a line terminator when <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition that controls whether the lines are appended.</param>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteLinesIf(bool condition, params string?[] lines)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(lines);
        if (condition) WriteLines((IEnumerable<string?>)lines);
    }

    /// <summary>
    /// Appends each line of a sequence followed by a line terminator when <paramref name="condition"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition that controls whether the lines are appended.</param>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteLinesIf(bool condition, IEnumerable<string?> lines)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(lines);
        if (condition) WriteLines(lines);
    }

    /// <summary>
    /// Appends text when running on Windows.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteIfWindows(string? text)
    {
        WriteIf(OperatingSystem.IsWindows(), text);
    }

    /// <summary>
    /// Appends a line when running on Windows.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteLineIfWindows(string? text)
    {
        WriteLineIf(OperatingSystem.IsWindows(), text);
    }

    /// <summary>
    /// Appends each line when running on Windows.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfWindows(params string?[] lines)
    {
        WriteLinesIf(OperatingSystem.IsWindows(), lines);
    }

    /// <summary>
    /// Appends each line of a sequence when running on Windows.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfWindows(IEnumerable<string?> lines)
    {
        WriteLinesIf(OperatingSystem.IsWindows(), lines);
    }

    /// <summary>
    /// Appends text when running on macOS.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteIfMacOS(string? text)
    {
        WriteIf(OperatingSystem.IsMacOS(), text);
    }

    /// <summary>
    /// Appends a line when running on macOS.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteLineIfMacOS(string? text)
    {
        WriteLineIf(OperatingSystem.IsMacOS(), text);
    }

    /// <summary>
    /// Appends each line when running on macOS.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfMacOS(params string?[] lines)
    {
        WriteLinesIf(OperatingSystem.IsMacOS(), lines);
    }

    /// <summary>
    /// Appends each line of a sequence when running on macOS.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfMacOS(IEnumerable<string?> lines)
    {
        WriteLinesIf(OperatingSystem.IsMacOS(), lines);
    }

    /// <summary>
    /// Appends text when running on Linux.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteIfLinux(string? text)
    {
        WriteIf(OperatingSystem.IsLinux(), text);
    }

    /// <summary>
    /// Appends a line when running on Linux.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteLineIfLinux(string? text)
    {
        WriteLineIf(OperatingSystem.IsLinux(), text);
    }

    /// <summary>
    /// Appends each line when running on Linux.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfLinux(params string?[] lines)
    {
        WriteLinesIf(OperatingSystem.IsLinux(), lines);
    }

    /// <summary>
    /// Appends each line of a sequence when running on Linux.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfLinux(IEnumerable<string?> lines)
    {
        WriteLinesIf(OperatingSystem.IsLinux(), lines);
    }

    /// <summary>
    /// Appends text when running on Linux, macOS, or FreeBSD.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteIfUnix(string? text)
    {
        WriteIf(IsUnixPlatform(), text);
    }

    /// <summary>
    /// Appends a line when running on Linux, macOS, or FreeBSD.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void WriteLineIfUnix(string? text)
    {
        WriteLineIf(IsUnixPlatform(), text);
    }

    /// <summary>
    /// Appends each line when running on Linux, macOS, or FreeBSD.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfUnix(params string?[] lines)
    {
        WriteLinesIf(IsUnixPlatform(), lines);
    }

    /// <summary>
    /// Appends each line of a sequence when running on Linux, macOS, or FreeBSD.
    /// </summary>
    /// <param name="lines">The lines to append. An empty sequence appends nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public void WriteLinesIfUnix(IEnumerable<string?> lines)
    {
        WriteLinesIf(IsUnixPlatform(), lines);
    }

    #endregion

    #region Shell syntax helpers

    /// <summary>
    /// Appends one or more comment lines using the current platform's shell syntax.
    /// </summary>
    /// <param name="text">The comment text. Every line receives a comment prefix.</param>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteComment(string? text)
    {
        ThrowIfDisposed();
        AppendComment(text);
    }

    /// <summary>
    /// Appends each comment line using the current platform's shell syntax.
    /// </summary>
    /// <param name="lines">
    /// The comment lines. An empty sequence appends nothing, and an entry that itself spans several lines receives a
    /// comment prefix on each of them.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteComments(params string?[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        WriteComments((IEnumerable<string?>)lines);
    }

    /// <summary>
    /// Appends each comment line of a sequence using the current platform's shell syntax.
    /// </summary>
    /// <param name="lines">
    /// The comment lines. An empty sequence appends nothing, and an entry that itself spans several lines receives a
    /// comment prefix on each of them.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteComments(IEnumerable<string?> lines)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines) AppendComment(line);
    }

    /// <summary>
    /// Appends a command that sets and exports an environment variable for subsequent script commands.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid variable name.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void WriteEnvironmentVariable(string name, string? value)
    {
        WriteLine(FormatEnvironmentVariableAssignment(name, value));
    }

    /// <summary>
    /// Formats an environment-variable reference for the current platform.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns><c>%NAME%</c> on Windows or <c>${NAME}</c> on other platforms.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid variable name.</exception>
    public static string FormatVariable(string name)
    {
        ValidateVariableName(name);
        return OperatingSystem.IsWindows() ? $"%{name}%" : $"${{{name}}}";
    }

    /// <summary>
    /// Formats a command that sets and exports an environment variable for the current platform.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    /// <returns>A Windows batch <c>set</c> command or Unix <c>export</c> command.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid variable name.</exception>
    public static string FormatEnvironmentVariableAssignment(string name, string? value)
    {
        ValidateVariableName(name);
        return OperatingSystem.IsWindows()
            ? $"set \"{name}={value.EscapeWindowsBatchValue()}\""
            : $"export {name}={value.QuoteBashAnsiCString()}";
    }

    #endregion

    #region Flush

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <remarks>Atomically writes the buffered script to <see cref="FilePath"/>.</remarks>
    public override void Flush()
    {
        ThrowIfDisposed();
        WriteScript();
    }

    /// <inheritdoc />
    public override Task FlushAsync()
    {
        return FlushAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <remarks>Atomically writes the buffered script to <see cref="FilePath"/>.</remarks>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return WriteScriptAsync(cancellationToken);
    }

    #endregion

    #region Execute

    /// <summary>
    /// Writes and executes the script with raw arguments, capturing its exit code, standard output, and standard error.
    /// </summary>
    /// <param name="arguments">The raw arguments to pass to the script.</param>
    /// <returns>The completed process output.</returns>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public ProcessOutput Execute(string? arguments = null)
    {
        EnsureScriptWritten();
        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(FilePath, arguments, RequireElevation);
        return GetProcessOutput(startInfo);
    }

    /// <summary>
    /// Writes and executes the script with an argument list, capturing its exit code, standard output, and standard
    /// error.
    /// </summary>
    /// <param name="arguments">The arguments to pass to the script.</param>
    /// <returns>The completed process output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public ProcessOutput Execute(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        EnsureScriptWritten();
        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(FilePath, arguments, RequireElevation);
        return GetProcessOutput(startInfo);
    }

    /// <summary>
    /// Asynchronously writes and executes the script with raw arguments, capturing its exit code, standard output,
    /// and standard error.
    /// </summary>
    /// <param name="arguments">The raw arguments to pass to the script.</param>
    /// <param name="cancellationToken">The token used to cancel the file write or running process.</param>
    /// <returns>A task containing the completed process output.</returns>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<ProcessOutput> ExecuteAsync(
        string? arguments = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureScriptWrittenAsync(cancellationToken).ConfigureAwait(false);
        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(FilePath, arguments, RequireElevation);
        return await GetProcessOutputAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes and executes the script with an argument list, capturing its exit code, standard output,
    /// and standard error.
    /// </summary>
    /// <param name="arguments">The arguments to pass to the script.</param>
    /// <param name="cancellationToken">The token used to cancel the file write or running process.</param>
    /// <returns>A task containing the completed process output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<ProcessOutput> ExecuteAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        await EnsureScriptWrittenAsync(cancellationToken).ConfigureAwait(false);
        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(FilePath, arguments, RequireElevation);
        return await GetProcessOutputAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Overrides

    /// <inheritdoc />
    /// <returns>The script file path.</returns>
    public override string ToString()
    {
        return FilePath;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Writes pending content when the file is kept, then deletes the file when <see cref="DeleteOnDispose"/> is
    /// enabled.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;

            if (disposing)
            {
                try
                {
                    if (!DeleteOnDispose && _isFlushPending) WriteScript();
                }
                finally
                {
                    DeleteFileIfRequested();
                }
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Writes pending content when the file is kept, then deletes the file when <see cref="DeleteOnDispose"/> is
    /// enabled.
    /// </remarks>
    public override async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;

            try
            {
                if (!DeleteOnDispose && _isFlushPending)
                    await WriteScriptAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                DeleteFileIfRequested();
            }
        }

        base.Dispose(true);
    }

    #endregion

    #region Private methods

    private void EnsureScriptWritten()
    {
        ThrowIfDisposed();
        if (_isFlushPending || !Exists) WriteScript();
    }

    private Task EnsureScriptWrittenAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _isFlushPending || !Exists
            ? WriteScriptAsync(cancellationToken)
            : Task.CompletedTask;
    }

    private void WriteScript()
    {
        SafeFile.WriteAllText(FilePath, _builder.ToString(), Utf8WithoutBom);
        ApplyExecutablePermission();
        _isFlushPending = false;
    }

    private async Task WriteScriptAsync(CancellationToken cancellationToken)
    {
        await SafeFile.WriteAllTextAsync(FilePath, _builder.ToString(), Utf8WithoutBom, cancellationToken)
            .ConfigureAwait(false);
        ApplyExecutablePermission();
        _isFlushPending = false;
    }

    private void ApplyExecutablePermission()
    {
        if (!SetExecutablePermission || OperatingSystem.IsWindows()) return;

        try
        {
            UnixSystem.SetUnix755Executable(FilePath);
        }
        catch (Exception exception)
        {
            // Best effort only; the shell reads the script file directly.
            Debug.WriteLine(exception);
        }
    }

    private void DeleteFileIfRequested()
    {
        if (!DeleteOnDispose) return;

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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void AppendComment(string? text)
    {
        var prefix = OperatingSystem.IsWindows() ? "rem " : "# ";
        foreach (var line in (text ?? string.Empty).ReplaceLineEndings("\n").Split('\n'))
            _builder.Append(prefix).Append(line).Append(CoreNewLine);

        _isFlushPending = true;
    }

    private void AppendPlatformPreamble()
    {
        _builder.Append(OperatingSystem.IsWindows() ? "@echo off" : "#!/usr/bin/env bash").Append(CoreNewLine);
        _isFlushPending = true;
    }

    private static ProcessOutput GetProcessOutput(ProcessStartInfo startInfo)
    {
        if (UsesWindowsRunAs(startInfo))
        {
            var exitCode = ProcessHelper.StartProcess(startInfo, true);
            return new ProcessOutput(exitCode, string.Empty, string.Empty);
        }

        return ProcessHelper.GetProcessOutput(startInfo);
    }

    private static async Task<ProcessOutput> GetProcessOutputAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        if (UsesWindowsRunAs(startInfo))
        {
            var exitCode = await ProcessHelper.StartProcessAsync(
                    startInfo,
                    true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new ProcessOutput(exitCode, string.Empty, string.Empty);
        }

        return await ProcessHelper.GetProcessOutputAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    private static bool UsesWindowsRunAs(ProcessStartInfo startInfo)
    {
        return OperatingSystem.IsWindows() &&
               string.Equals(startInfo.Verb, "runas", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnixPlatform()
    {
        return !OperatingSystem.IsWindows();
    }

    private static void ValidateVariableName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name[0] != '_' && !char.IsAsciiLetter(name[0]))
        {
            ThrowInvalidVariableName(name);
        }

        for (var i = 1; i < name.Length; i++)
        {
            var character = name[i];
            if (character != '_' && !char.IsAsciiLetter(character) && character is not (>= '0' and <= '9'))
                ThrowInvalidVariableName(name);
        }
    }

    private static void ThrowInvalidVariableName(string name)
    {
        throw new ArgumentException(
            $"'{name}' is not a valid variable name. Names must start with an ASCII letter or underscore and contain only ASCII letters, digits, or underscores.",
            nameof(name));
    }

    #endregion
}