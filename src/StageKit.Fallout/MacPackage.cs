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
        var uncompressedImagePath = string.Concat(outputPath, ".uncompressed.dmg");
        var quotedOutputPath = outputPath.QuoteShell();
        return $"ln -s {"/Applications".QuoteShell()} {applicationsLinkPath.QuoteShell()} && " +
               $"uncompressed_image={uncompressedImagePath.QuoteShell()} && " +
               "trap 'rm -f \"$uncompressed_image\"' EXIT && " +
               $"hdiutil create -volname {volumeName.QuoteShell()} -srcfolder {sourceFolderPath.QuoteShell()} " +
               "-ov -format UDRW \"$uncompressed_image\" && " +
               "hdiutil convert \"$uncompressed_image\" -ov -format UDZO -tasks 1 " +
               $"-o {quotedOutputPath}; conversion_status=$?; " +
               $"if [ \"$conversion_status\" -eq 137 ] && [ -s {quotedOutputPath} ]; then " +
               "echo 'hdiutil exited with code 137 after creating the DMG; verifying the completed image.'; " +
               $"hdiutil verify {quotedOutputPath}; else exit \"$conversion_status\"; fi";
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
