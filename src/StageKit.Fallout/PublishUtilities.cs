using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fallout.Common.IO;
using StageKit.Primitives;

namespace StageKit.Fallout;

internal static class PublishUtilities
{
    private static readonly JsonSerializerOptions RuntimeManifestJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static AbsolutePath GetDirectChildPath(AbsolutePath directory, string childName, string valueName)
    {
        FileUtilities.ValidatePathLeafName(childName, valueName);

        var directoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var childPath = Path.GetFullPath(Path.Combine(directoryPath, childName));
        var childDirectory = Path.GetDirectoryName(childPath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(directoryPath, childDirectory, comparison))
            throw new InvalidOperationException($"{valueName} must resolve directly below '{directoryPath}'.");

        return childPath;
    }

    internal static void DeleteFilesByExtension(
        AbsolutePath directory,
        IEnumerable<string> extensions,
        string extensionsPropertyName)
    {
        if (!Directory.Exists(directory))
            return;

        var cleanupExtensions = extensions
            .Select(extension => NormalizeExtension(extension, extensionsPropertyName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (cleanupExtensions.Contains(Path.GetExtension(filePath).TrimStart('.')))
                File.Delete(filePath);
        }
    }

    internal static bool IsWixProject(string projectPath)
    {
        return string.Equals(Path.GetExtension(projectPath), ".wixproj", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension, string extensionsPropertyName)
    {
        var normalizedExtension = extension?.Trim().TrimStart('.') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedExtension))
        {
            throw new InvalidOperationException(
                $"{extensionsPropertyName} cannot contain blank extensions.");
        }

        return normalizedExtension;
    }

    internal static void CreateZip(AbsolutePath source, AbsolutePath destination)
    {
        ZipFile.CreateFromDirectory(source, destination, CompressionLevel.SmallestSize,
            false);
    }

    internal static void WriteRuntimeManifest(AbsolutePath directory, string fileName, BuildRuntime runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(runtime);

        if (!FileUtilities.IsPathLeafName(fileName))
            throw new ArgumentException("The runtime manifest path must be a file name.", nameof(fileName));

        // A validated leaf name always resolves directly below the directory, so this cannot throw.
        var manifestPath = GetDirectChildPath(directory, fileName, nameof(fileName));

        Directory.CreateDirectory(manifestPath.Parent);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(runtime, RuntimeManifestJsonOptions));
    }

    /// <summary>
    /// Escapes characters with syntactic meaning in MSBuild command-line property values (<c>%</c>, <c>;</c>, <c>,</c>)
    /// using standard MSBuild URL percent-encoding, so MSBuild unescapes them during property evaluation.
    /// </summary>
    /// <param name="value">The raw property value to escape.</param>
    /// <returns>The MSBuild-escaped property value.</returns>
    internal static string EscapeMSBuildPropertyValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace(";", "%3B", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
    }

    /// <summary>
    /// Formats a collection of file extensions, patterns, or Advanced Query Syntax (AQS) strings into a Windows Explorer
    /// <c>AppliesTo</c> query expression.
    /// </summary>
    /// <param name="filesOrPatterns">The raw file extensions, patterns, or query strings.</param>
    /// <returns>The formatted <c>AppliesTo</c> query expression, or an empty string if blank or wildcard.</returns>
    internal static string FormatContextMenuAppliesTo(IEnumerable<string>? filesOrPatterns)
    {
        if (filesOrPatterns is null)
            return string.Empty;

        var patterns = new List<string>();
        foreach (var item in filesOrPatterns)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            var trimmed = item.Trim();
            if (trimmed == "*")
                return string.Empty;

            var tokens = trimmed.Split([';', ','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawToken in tokens)
            {
                var token = rawToken.Trim();
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (token == "*")
                    return string.Empty;

                if (token.Contains("System.FileName:", StringComparison.OrdinalIgnoreCase))
                {
                    patterns.Add(token);
                    continue;
                }

                string pattern;
                if (token.StartsWith("*.", StringComparison.Ordinal))
                    pattern = token;
                else if (token.StartsWith(".", StringComparison.Ordinal))
                    pattern = "*" + token;
                else if (token.StartsWith("*", StringComparison.Ordinal))
                    pattern = token;
                else
                    pattern = "*." + token;

                patterns.Add($"System.FileName:\"{pattern}\"");
            }
        }

        return patterns.Count == 0 ? string.Empty : string.Join(" OR ", patterns);
    }

    /// <summary>
    /// Formats a list of file extensions, patterns, or an Advanced Query Syntax (AQS) string into a Windows Explorer
    /// <c>AppliesTo</c> query expression.
    /// </summary>
    /// <param name="filesOrPatterns">The raw file extensions, patterns, or query string.</param>
    /// <returns>The formatted <c>AppliesTo</c> query expression, or an empty string if blank or wildcard.</returns>
    internal static string FormatContextMenuAppliesTo(string? filesOrPatterns)
    {
        if (string.IsNullOrWhiteSpace(filesOrPatterns))
            return string.Empty;

        return FormatContextMenuAppliesTo([filesOrPatterns]);
    }
}
