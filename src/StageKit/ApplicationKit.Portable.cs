using StageKit.Primitives;
using StageKit.Runtime;

namespace StageKit;

public static partial class ApplicationKit
{
    /// <summary>
    /// Gets or sets a value that indicates whether the application is running in portable mode.
    /// </summary>
    /// <remarks>
    /// <see cref="ParseProfilePathFromArgs"/> sets this value to <see langword="true"/> only when
    /// --portable parsing succeeds. A custom profile path does not enable portable mode.
    /// </remarks>
    public static bool IsPortable { get; set; }

    /// <summary>
    /// Gets or sets the directory name used for the portable profile.
    /// </summary>
    /// <remarks>Make sure to set this variable before calling <see cref="ParseCrashReportArgs"/>.<br/>
    /// <see cref="ApplicationName"/>_ will be prepended when folder not at same level as <see cref="EntryApplication.ProcessPath"/>.</remarks>
    public static string PortableProfileDirectoryName { get; set; } = "configs";

    /// <summary>
    /// Parses command-line arguments that can update the active profile path.
    /// </summary>
    /// <param name="args">The arguments to parse. Defaults to <see cref="ApplicationArgs"/>.</param>
    /// <param name="profilePathArg">The argument name whose following value contains the requested profile path, use <see langword="null"/> to disable.</param>
    /// <param name="portableArg">The argument name that enables portable mode with an optional parent-directory level, use <see langword="null"/> to disable.</param>
    public static void ParseProfilePathFromArgs(
        string[]? args = null,
        string? profilePathArg = "--profile-path",
        string? portableArg = "--portable")
    {
        args ??= ApplicationArgs;
        if (args is null) return;

        if (!string.IsNullOrWhiteSpace(portableArg))
        {
            var portableIndex = args.LastIndexOf(portableArg);
            if (portableIndex >= 0 && TryGetPortableProfilePath(args, portableIndex, out var portableProfilePath))
            {
                ProfilePath = portableProfilePath;
                IsPortable = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(profilePathArg))
        {
            var profilePathIndex = args.LastIndexOf(profilePathArg);
            if (profilePathIndex >= 0 && args.Length > profilePathIndex + 1)
            {
                var profilePath = args[profilePathIndex + 1];

                if (!string.IsNullOrWhiteSpace(profilePath) &&
                    PathUtilities.IsWritableOrCreatableDirectory(profilePath, out var fullProfilePath))
                {
                    ProfilePath = fullProfilePath;
                }
            }
        }
    }

    private static bool TryGetPortableProfilePath(string[] args, int portableIndex, out string portableProfilePath)
    {
        portableProfilePath = string.Empty;
        var portableLevel = 0;
        if (args.Length > portableIndex + 1 && int.TryParse(args[portableIndex + 1], out var parsedPortableLevel))
        {
            if (parsedPortableLevel < 0) return false;
            portableLevel = parsedPortableLevel;
        }

        var portableRootPath = EntryApplication.BaseDirectory ?? EntryApplication.AppContextBaseDirectory;
        for (var parentIndex = 0; parentIndex < portableLevel; parentIndex++)
        {
            var parent = Directory.GetParent(portableRootPath);
            if (parent is null) return false;

            portableRootPath = parent.FullName;
        }

        portableRootPath = portableRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var configurationPath = portableRootPath ==
                                EntryApplication.AppContextBaseDirectory.TrimEnd(Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar)
            ? Path.Combine(portableRootPath, PortableProfileDirectoryName)
            : Path.Combine(portableRootPath, $"{ApplicationName}_{PortableProfileDirectoryName}");
        if (!PathUtilities.TryGetValidFullDirectoryPath(configurationPath, out var fullConfigurationPath)) return false;

        try
        {
            Directory.CreateDirectory(fullConfigurationPath);
            portableProfilePath = fullConfigurationPath;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                       or UnauthorizedAccessException)
        {
            return false;
        }
    }
}