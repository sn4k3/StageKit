namespace StageKit.Primitives;

/// <summary>
/// Provides cross-platform system helper methods.
/// </summary>
public static class SystemUtilities
{
    /// <summary>
    /// Gets the string comparison used for file-system paths on the current platform.
    /// </summary>
    public static StringComparison SystemStringComparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}