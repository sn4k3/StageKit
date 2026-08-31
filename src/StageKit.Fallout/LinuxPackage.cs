using System.Text.RegularExpressions;
using Fallout.Common.Utilities;
using StageKit.Primitives.Extensions;

namespace StageKit.Fallout;

/// <summary>
/// Generates validated metadata for native Linux distribution packages.
/// </summary>
internal static partial class LinuxPackage
{
    internal static string GetPackageName(string softwareName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(softwareName);
        var packageName = new string(softwareName
            .ToLowerInvariant()
            .Select(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '.'
                ? character
                : '-')
            .ToArray()).Trim('-', '.');
        packageName = RepeatedHyphenRegex().Replace(packageName, "-");
        if (packageName.Length < 2 || packageName[0] is not (>= 'a' and <= 'z' or >= '0' and <= '9'))
        {
            throw new InvalidOperationException(
                $"Software name '{softwareName}' cannot be converted to a valid Linux package name.");
        }

        return packageName;
    }

    internal static string GetSnapName(string softwareName)
    {
        var snapName = GetPackageName(softwareName)
            .Replace('+', '-')
            .Replace('.', '-');
        snapName = RepeatedHyphenRegex().Replace(snapName, "-").Trim('-');
        if (snapName.Length > 40 || !snapName.Any(character => character is >= 'a' and <= 'z'))
            throw new InvalidOperationException($"Software name '{softwareName}' is not a valid Snap name.");
        return snapName;
    }

    internal static string GetDebianControl(string packageName, string version, string architecture,
        string maintainer, string summary, string description)
    {
        ValidateSimpleValue(packageName, nameof(packageName));
        ValidateSimpleValue(version, nameof(version));
        ValidateSimpleValue(architecture, nameof(architecture));
        ValidateMaintainer(maintainer);
        summary = NormalizeSingleLine(summary, nameof(summary));
        description = NormalizeSingleLine(description, nameof(description));
        return $"Package: {packageName}\nVersion: {version}\nArchitecture: {architecture}\n" +
               $"Maintainer: {maintainer}\nDescription: {summary}\n {description}\n";
    }

    internal static string GetRpmSpec(string packageName, string version, string architecture, string license,
        string summary, string description, string payloadPath)
    {
        ValidateSimpleValue(packageName, nameof(packageName));
        ValidateSimpleValue(version, nameof(version));
        ValidateSimpleValue(architecture, nameof(architecture));
        license = NormalizeRpmValue(license, nameof(license));
        summary = NormalizeRpmValue(summary, nameof(summary));
        description = NormalizeRpmValue(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadPath);
        var quotedPayload = $"{payloadPath.TrimEnd('/', '\\')}/.".QuoteShell();
        return $"Name: {packageName}\nVersion: {version}\nRelease: 1\nSummary: {summary}\n" +
               $"License: {license}\nBuildArch: {architecture}\n\n%description\n{description}\n\n" +
               $"%install\nmkdir -p \"$RPM_BUILD_ROOT\"\ncp -a {quotedPayload} \"$RPM_BUILD_ROOT/\"\n\n" +
               "%files\n/usr/bin/*\n/usr/lib/*\n/usr/share/applications/*\n/usr/share/icons/hicolor/*/apps/*\n" +
               "/usr/share/metainfo/*\n";
    }

    internal static string GetArchPkgBuild(string packageName, string version, string architecture, string license,
        string summary, string sourceDirectoryName)
    {
        ValidateSimpleValue(packageName, nameof(packageName));
        ValidateSimpleValue(version, nameof(version));
        ValidateSimpleValue(architecture, nameof(architecture));
        ValidateSimpleValue(license, nameof(license));
        ValidateSimpleValue(sourceDirectoryName, nameof(sourceDirectoryName));
        summary = NormalizeSingleLine(summary, nameof(summary));
        var escapedSourceDirectoryName = sourceDirectoryName.EscapeBashDoubleQuoted();
        return $"pkgname={packageName}\npkgver={version}\npkgrel=1\npkgdesc={summary.QuoteShell()}\n" +
               $"arch=({architecture.QuoteShell()})\nlicense=({license.QuoteShell()})\noptions=('!strip' '!debug')\n" +
               "source=(\"$pkgname-$pkgver.tar.gz\")\nsha256sums=('SKIP')\n" +
               $"package() {{ cp -a \"$srcdir/{escapedSourceDirectoryName}/usr\" \"$pkgdir/\"; }}\n";
    }

    internal static string GetSnapcraftManifest(string packageName, string version, string buildArchitecture,
        string targetArchitecture, string executableName,
        string summary, string description, string snapBase, string confinement, IReadOnlyCollection<string> plugs)
    {
        ValidateSimpleValue(packageName, nameof(packageName));
        ValidateSimpleValue(version, nameof(version));
        ValidateSimpleValue(buildArchitecture, nameof(buildArchitecture));
        ValidateSimpleValue(targetArchitecture, nameof(targetArchitecture));
        ValidateSimpleValue(executableName, nameof(executableName));
        ValidateSimpleValue(snapBase, nameof(snapBase));
        ValidateSimpleValue(confinement, nameof(confinement));
        summary = NormalizeSingleLine(summary, nameof(summary));
        description = NormalizeSingleLine(description, nameof(description));
        ArgumentNullException.ThrowIfNull(plugs);
        if (plugs.Count == 0 || plugs.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty Snap plug is required.", nameof(plugs));
        foreach (var plug in plugs)
            ValidateSimpleValue(plug, nameof(plugs));
        var plugLines = string.Join('\n', plugs.Select(plug => $"      - {plug.SingleQuoteYaml()}"));
        var architectureBlock = GetSnapArchitectureBlock(snapBase, buildArchitecture, targetArchitecture);
        return $$"""
                 name: {{packageName.SingleQuoteYaml()}}
                 base: {{snapBase.SingleQuoteYaml()}}
                 version: {{version.SingleQuoteYaml()}}
                 summary: {{summary.SingleQuoteYaml()}}
                 description: {{description.SingleQuoteYaml()}}
                 grade: stable
                 confinement: {{confinement.SingleQuoteYaml()}}
                 {{architectureBlock}}
                 apps:
                   {{packageName}}:
                     command: {{executableName.SingleQuoteYaml()}}
                     plugs:
                 {{plugLines}}
                 parts:
                   application:
                     plugin: dump
                     source: payload
                 """.ReplaceLineEndings("\n");
    }

    internal static string GetDebianVersion(string version)
    {
        return ValidateVersion(version, "Debian", ".+~-:");
    }

    internal static string GetRpmVersion(string version)
    {
        return ValidateVersion(version.Replace('-', '~'), "RPM", ".+~^_");
    }

    internal static string GetArchVersion(string version)
    {
        return ValidateVersion(version.Replace('-', '_').Replace(':', '_').Replace('/', '_'), "Arch Linux", ".+~^_");
    }

    private static string ValidateVersion(string version, string format, string allowedPunctuation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (!char.IsAsciiDigit(version[0]) || version.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && !allowedPunctuation.Contains(character)))
        {
            throw new InvalidOperationException($"Version '{version}' is not valid for {format} packaging.");
        }

        return version;
    }

    private static void ValidateMaintainer(string maintainer)
    {
        ValidateSimpleValue(maintainer, nameof(maintainer));
        if (!MaintainerRegex().IsMatch(maintainer))
        {
            throw new InvalidOperationException(
                $"{nameof(LinuxAppBundleOptions.DebPackageMaintainer)} must use the form 'Full Name <email@example.com>'.");
        }
    }

    private static void ValidateSimpleValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.ContainsAny('\r', '\n'))
            throw new ArgumentException("Package metadata cannot contain line breaks.", parameterName);
    }

    private static string NormalizeSingleLine(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return string.Join(' ',
            value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeRpmValue(string value, string parameterName)
    {
        var normalized = NormalizeSingleLine(value, parameterName);
        return normalized.Replace("%", "%%", StringComparison.Ordinal);
    }

    private static string GetSnapArchitectureBlock(string snapBase, string buildArchitecture,
        string targetArchitecture)
    {
        var usesLegacyArchitectures = snapBase.StartsWith("core", StringComparison.Ordinal) &&
                                      int.TryParse(snapBase.AsSpan(4), out var baseVersion) &&
                                      baseVersion <= 22;
        return usesLegacyArchitectures
            ? $"architectures:\n  - build-on: [{buildArchitecture.SingleQuoteYaml()}]\n" +
              $"    build-for: [{targetArchitecture.SingleQuoteYaml()}]"
            : $"platforms:\n  {targetArchitecture}:\n" +
              $"    build-on: [{buildArchitecture.SingleQuoteYaml()}]\n" +
              $"    build-for: [{targetArchitecture.SingleQuoteYaml()}]";
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedHyphenRegex();

    [GeneratedRegex("^[^<>\\r\\n]+ <[^<>@\\s]+@[^<>@\\s]+>$", RegexOptions.CultureInvariant)]
    private static partial Regex MaintainerRegex();
}