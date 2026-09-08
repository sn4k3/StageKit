using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace StageKit.Primitives;

/// <summary>
/// Provides cross-platform file helper methods.
/// </summary>
public static class FileUtilities
{
    /// <summary>
    /// 
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public static SearchValues<char> InvalidFileNameChars => field ??=
        SearchValues.Create(Path.GetInvalidFileNameChars());

    /// <summary>
    /// Determines whether a value names a single file-system entry rather than a path.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><c>true</c> when the value is a nonblank simple leaf name; otherwise, <c>false</c>.</returns>
    public static bool IsPathLeafName([NotNullWhen(true)] string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !Path.IsPathRooted(value) &&
               !value.Contains('/') &&
               !value.Contains('\\') &&
               value is not ("." or "..") &&
               value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    /// <summary>
    /// Validates that a value names a single file-system entry rather than a path.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="valueName">The name of the value being validated.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a valid leaf name.</exception>
    public static string ValidatePathLeafName(string? value, string valueName)
    {
        return !IsPathLeafName(value)
            ? throw new InvalidOperationException($"{valueName} must be a nonblank simple leaf name.")
            : value;
    }

    /// <summary>
    /// Sanitizes a file name by replacing invalid characters with an underscore.
    /// </summary>
    /// <param name="filename">The file name to sanitize.</param>
    /// <param name="replacementChar">The replacement char to use to replace invalid characters.</param>
    /// <returns>
    /// The original <paramref name="filename"/> if it contains no invalid characters;
    /// otherwise, a new string with invalid characters replaced with underscores.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filename"/> is <c>null</c>.</exception>
    public static string SanitizeFileName(string filename, char replacementChar = '_')
    {
        ArgumentNullException.ThrowIfNull(filename);

        var firstInvalidIndex = filename.AsSpan().IndexOfAny(InvalidFileNameChars);
        if (firstInvalidIndex < 0)
        {
            return filename;
        }

        return string.Create(filename.Length, (filename, firstInvalidIndex, replacementChar), static (span, state) =>
        {
            var (source, firstInvalid, replacementChar) = state;
            source.AsSpan(0, firstInvalid).CopyTo(span);
            span[firstInvalid] = replacementChar;

            for (var i = firstInvalid + 1; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = InvalidFileNameChars.Contains(c) ? replacementChar : c;
            }
        });
    }
}