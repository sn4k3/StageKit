using System.Reflection;

namespace StageKit;

internal static class Helpers
{
    private static readonly Lazy<Assembly?> EntryAssemblyLazy = new(Assembly.GetEntryAssembly);

    private static Assembly? EntryAssembly => EntryAssemblyLazy.Value;

    /// <summary>
    /// Provides lazy initialization for retrieving the informational version of the entry assembly, if available.
    /// </summary>
    /// <remarks>The informational version is typically specified using the
    /// AssemblyInformationalVersionAttribute in the assembly metadata. The value is retrieved only once, on first
    /// access, and cached for subsequent calls. If the entry assembly does not define an informational version, the
    /// value will be null.</remarks>
    private static readonly Lazy<string?> AssemblyInformationalVersionLazy = new(() =>
        EntryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Gets the entry assembly informational version, as specified by the <see cref="AssemblyInformationalVersionAttribute"/>
    /// </summary>
    internal static string? AssemblyInformationalVersion => AssemblyInformationalVersionLazy.Value;

    /// <summary>
    /// Provides lazy initialization of the version information for the entry assembly.
    /// </summary>
    /// <remarks>The value is retrieved from the entry assembly's metadata when first accessed. If the entry
    /// assembly is not available, the value will be null.</remarks>
    private static readonly Lazy<Version?> AssemblyVersionLazy = new(() =>
        EntryAssembly?.GetName().Version);

    /// <summary>
    /// Gets the entry assembly version, as specified by the <see cref="AssemblyVersionAttribute"/>.<br />
    /// </summary>
    /// <example>1.0.0.0</example>
    internal static Version? AssemblyVersion => AssemblyVersionLazy.Value;

    /// <summary>
    /// Lazily retrieves the version string of the entry assembly, excluding any build metadata if present.
    /// </summary>
    /// <remarks>The version string is determined by checking AssemblyInformationalVersion first, then
    /// AssemblyVersion. If the version string contains build metadata (indicated by a '+' character), only the
    /// portion before the '+' is returned. Returns null if no version information is available.</remarks>
    private static readonly Lazy<string?> AssemblyVersionStringLazy = new(() =>
    {
        var version = AssemblyInformationalVersion
                      ?? AssemblyVersion?.ToString();
        if (version is null) return null;
        var indexOf = version.IndexOf('+');
        return indexOf < 0 ? version : version[..indexOf];
    });

    /// <summary>
    /// Gets the entry assembly version as a string, preferring the informational version if available, and excluding any build metadata. For example, if the informational version is "1.0.0+build.123", this property will return "1.0.0".
    /// </summary>
    internal static string? AssemblyVersionString => AssemblyVersionStringLazy.Value;

    /// <summary>
    /// Translate numeric file size in bytes to a human-readable shorter string format.
    /// </summary>
    /// <param name="size">File size in bytes.</param>
    /// <returns>Returns file size short string.</returns>
    internal static string ToFileSizeString(long size)
    {
        const long KB = 1024L;
        const long MB = KB * 1024;
        const long GB = MB * 1024;
        const long TB = GB * 1024;
        const long PB = TB * 1024;
        const long EB = PB * 1024;

        if (size < KB) return size + " bytes";
        if (size < MB) return (size / (float)KB).ToString("F1") + " KB";
        if (size < GB) return (size / (float)MB).ToString("F1") + " MB";
        if (size < TB) return (size / (float)GB).ToString("F1") + " GB";
        if (size < PB) return (size / (float)TB).ToString("F1") + " TB";
        if (size < EB) return (size / (float)PB).ToString("F1") + " PB";
        return (size / (float)EB).ToString("F1") + " EB";
    }
}