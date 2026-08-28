using System.Diagnostics.CodeAnalysis;

namespace StageKit.Primitives;

/// <summary>
/// Provides cross-platform file helper methods.
/// </summary>
public static class FileUtilities
{
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
}