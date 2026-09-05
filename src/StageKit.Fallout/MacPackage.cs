using StageKit.Primitives.Extensions;

namespace StageKit.Fallout;

/// <summary>
/// Generates commands for native macOS distribution packages.
/// </summary>
internal static class MacPackage
{
    internal static string GetDmgCommand(string volumeName, string appPath, string outputPath)
    {
        ValidateValue(volumeName, nameof(volumeName));
        ValidateValue(appPath, nameof(appPath));
        ValidateValue(outputPath, nameof(outputPath));

        // The application bundle is passed directly as the source folder. Staging the bundle alongside an
        // '/Applications' symlink makes hdiutil follow that symlink while it scans the source tree, which walks the
        // whole host '/Applications' directory and gets hdiutil killed with exit code 137 on hosted macOS runners.
        return $"hdiutil create -volname {volumeName.QuoteShell()} -srcfolder {appPath.QuoteShell()} " +
               $"-ov -format UDZO {outputPath.QuoteShell()}";
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
