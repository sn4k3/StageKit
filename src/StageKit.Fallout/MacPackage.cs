using StageKit.Primitives.Extensions;

namespace StageKit.Fallout;

/// <summary>
/// Generates commands for native macOS distribution packages.
/// </summary>
internal static class MacPackage
{
    internal static string GetDmgCommand(string volumeName, string sourceFolderPath, string applicationsLinkPath,
        string outputPath)
    {
        ValidateValue(volumeName, nameof(volumeName));
        ValidateValue(sourceFolderPath, nameof(sourceFolderPath));
        ValidateValue(applicationsLinkPath, nameof(applicationsLinkPath));
        ValidateValue(outputPath, nameof(outputPath));
        return $"ln -s {"/Applications".QuoteShell()} {applicationsLinkPath.QuoteShell()} && " +
               $"rm -f {outputPath.QuoteShell()} && " +
               $"hdiutil makehybrid -hfs -hfs-volume-name {volumeName.QuoteShell()} " +
               $"-o {outputPath.QuoteShell()} {sourceFolderPath.QuoteShell()}";
    }

    internal static string GetPkgCommand(string appPath, string bundleIdentifier, string version, string outputPath)
    {
        ValidateValue(appPath, nameof(appPath));
        ValidateValue(bundleIdentifier, nameof(bundleIdentifier));
        ValidateValue(version, nameof(version));
        ValidateValue(outputPath, nameof(outputPath));
        return $"pkgbuild --component {appPath.QuoteShell()} --identifier {bundleIdentifier.QuoteShell()} " +
               $"--version {version.QuoteShell()} --install-location {"/Applications".QuoteShell()} " +
               outputPath.QuoteShell();
    }

    private static void ValidateValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.ContainsAny('\r', '\n'))
            throw new ArgumentException("Package command values cannot contain line breaks.", parameterName);
    }
}
