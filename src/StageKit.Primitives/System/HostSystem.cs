namespace StageKit.Primitives.System;

/// <summary>
/// Provides cross-platform system helper methods.
/// </summary>
public static class HostSystem
{
    /// <summary>
    /// Gets the string comparison used for file-system paths on the current platform.
    /// </summary>
    public static StringComparison HostStringComparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}