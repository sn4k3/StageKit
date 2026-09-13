using System.Collections.Immutable;
using Fallout.Common.IO;

namespace StageKit.Fallout;

/// <summary>
/// Defines an immutable file type association configured across supported packaging platforms (Windows, macOS, and Linux).
/// </summary>
public record FileAssociation
{
    private readonly ImmutableHashSet<string> _extensions =
        ImmutableHashSet<string>.Empty.WithComparer(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAssociation"/> record.
    /// </summary>
    public FileAssociation()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAssociation"/> record with the specified extension, name, and MIME type.
    /// </summary>
    /// <param name="extension">The file extension or pattern (e.g. <c>.sl1</c>, <c>sl1</c>, or <c>*.sl1</c>).</param>
    /// <param name="name">The human-readable name or description for the document type.</param>
    /// <param name="mimeType">The MIME type associated with the file type.</param>
    public FileAssociation(string extension, string? name = null, string? mimeType = null)
    {
        if (!string.IsNullOrWhiteSpace(extension))
        {
            _extensions = _extensions.Add(extension);
        }

        Name = name;
        MimeType = mimeType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAssociation"/> record with the specified extensions, name, and MIME type.
    /// </summary>
    /// <param name="extensions">The file extensions or patterns.</param>
    /// <param name="name">The human-readable name or description for the document type.</param>
    /// <param name="mimeType">The MIME type associated with the file type.</param>
    public FileAssociation(IEnumerable<string> extensions, string? name = null, string? mimeType = null)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions)
        {
            if (!string.IsNullOrWhiteSpace(extension))
            {
                builder.Add(extension);
            }
        }

        _extensions = builder.ToImmutable();
        Name = name;
        MimeType = mimeType;
    }

    /// <summary>
    /// Gets the file extension(s) or pattern(s) associated with this entry (e.g. <c>.sl1</c>, <c>sl1</c>, <c>*.sl1</c>).
    /// </summary>
    public ImmutableHashSet<string> Extensions
    {
        get => _extensions;
        init => _extensions = value.KeyComparer == StringComparer.OrdinalIgnoreCase
            ? value
            : ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, value);
    }

    /// <summary>
    /// Gets the human-readable name or description for this document type (e.g. <c>StageKit Project</c>).
    /// Used on macOS as <c>CFBundleTypeName</c> and on Windows as document type description.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the primary MIME type (e.g. <c>application/x-sl1</c> or <c>application/zip</c>).
    /// Used on Linux for desktop entry MIME types and AppStream metadata.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets the role of the application for this file type.
    /// Used on macOS as <c>CFBundleTypeRole</c>. The default is <see cref="DocumentRole.Editor"/>.
    /// </summary>
    public DocumentRole Role { get; init; } = DocumentRole.Editor;

    /// <summary>
    /// Gets the macOS handler rank.
    /// Used on macOS as <c>LSHandlerRank</c>. The default is <see cref="BundleHandlerRank.Default"/>.
    /// </summary>
    public BundleHandlerRank HandlerRank { get; init; } = BundleHandlerRank.Default;

    /// <summary>
    /// Gets the macOS Uniform Type Identifier (UTI) conforming content types (e.g. <c>public.data</c>).
    /// Used on macOS as <c>LSItemContentTypes</c>.
    /// </summary>
    public ImmutableList<string> ContentTypes { get; init; } = [];

    /// <summary>
    /// Gets the document type icon file.
    /// </summary>
    public AbsolutePath? IconFile { get; init; }

    /// <summary>
    /// Determines whether this instance and another specified <see cref="FileAssociation"/> object have the same value.
    /// </summary>
    /// <param name="other">The association to compare with this instance.</param>
    /// <returns><c>true</c> if the value of <paramref name="other"/> is the same as the value of this instance; otherwise, <c>false</c>.</returns>
    public virtual bool Equals(FileAssociation? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Extensions.SetEquals(other.Extensions) &&
               string.Equals(Name, other.Name, StringComparison.Ordinal) &&
               string.Equals(MimeType, other.MimeType, StringComparison.OrdinalIgnoreCase) &&
               Role == other.Role &&
               HandlerRank == other.HandlerRank &&
               ContentTypes.SequenceEqual(other.ContentTypes, StringComparer.Ordinal) &&
               IconFile == other.IconFile;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var ext in Extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
        {
            hash.Add(ext, StringComparer.OrdinalIgnoreCase);
        }

        hash.Add(Name);
        hash.Add(MimeType, StringComparer.OrdinalIgnoreCase);
        hash.Add(Role);
        hash.Add(HandlerRank);
        foreach (var type in ContentTypes)
        {
            hash.Add(type);
        }

        hash.Add(IconFile);
        return hash.ToHashCode();
    }
}
