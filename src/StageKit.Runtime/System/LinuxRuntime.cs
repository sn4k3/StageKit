using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace StageKit.Runtime.System;

/// <summary>
/// Provides information about the Linux runtime environment.
/// </summary>
[SupportedOSPlatform("linux")]
public static class LinuxRuntime
{
    /// <summary>
    /// Gets the package manager used by the Linux runtime environment.
    /// </summary>
    private static LinuxPackageManager? _packageManager;

    /// <summary>
    /// Gets the package manager used by the Linux runtime environment.
    /// </summary>
    public static LinuxPackageManager PackageManager => _packageManager ??= GetLinuxPackageManager();

    /// <summary>
    /// Gets the Linux distribution information.
    /// </summary>
    [field: MaybeNull]
    [field: AllowNull]
    public static LinuxDistribution Distribution => field ??= GetLinuxDistribution();

    /// <summary>
    /// Gets the Linux distribution information as a dictionary of key-value pairs.
    /// </summary>
    [field: MaybeNull]
    [field: AllowNull]
    public static Dictionary<string, string> OsRelease => field ??= GetOsRelease();

    /// <summary>
    /// Converts the specified <see cref="LinuxPackageManager"/> value to its corresponding command name.
    /// </summary>
    /// <param name="value">The <see cref="LinuxPackageManager"/> value.</param>
    /// <returns>The command name corresponding to the specified <see cref="LinuxPackageManager"/> value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <see cref="LinuxPackageManager"/> value is not recognized.</exception>
    public static string ToCommandName(this LinuxPackageManager value)
    {
        return value switch
        {
            LinuxPackageManager.Unknown => "unknown",
            LinuxPackageManager.Apt => "apt",
            LinuxPackageManager.Dnf5 => "dnf5",
            LinuxPackageManager.Dnf => "dnf",
            LinuxPackageManager.Yum => "yum",
            LinuxPackageManager.Zypper => "zypper",
            LinuxPackageManager.Pacman => "pacman",
            LinuxPackageManager.Apk => "apk",
            LinuxPackageManager.Xbps => "xbps-install",
            LinuxPackageManager.Emerge => "emerge",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    /// <summary>
    /// Gets the package manager used by the Linux runtime environment.
    /// </summary>
    /// <returns>The <see cref="LinuxPackageManager"/> used by the Linux runtime environment.</returns>
    private static LinuxPackageManager GetLinuxPackageManager()
    {
        if (!OperatingSystem.IsLinux())
            return LinuxPackageManager.Unknown;

        // Debian / Ubuntu
        if (File.Exists("/usr/bin/apt") ||
            File.Exists("/usr/bin/apt-get"))
            return LinuxPackageManager.Apt;

        // Fedora / RHEL
        if (File.Exists("/usr/bin/dnf5"))
            return LinuxPackageManager.Dnf5;

        if (File.Exists("/usr/bin/dnf"))
            return LinuxPackageManager.Dnf;

        if (File.Exists("/usr/bin/yum"))
            return LinuxPackageManager.Yum;

        // openSUSE / SUSE
        if (File.Exists("/usr/bin/zypper"))
            return LinuxPackageManager.Zypper;

        // Arch Linux
        if (File.Exists("/usr/bin/pacman"))
            return LinuxPackageManager.Pacman;

        // Gentoo
        if (File.Exists("/usr/bin/emerge"))
            return LinuxPackageManager.Emerge;

        // Alpine Linux
        if (File.Exists("/sbin/apk") ||
            File.Exists("/usr/sbin/apk") ||
            File.Exists("/usr/bin/apk"))
            return LinuxPackageManager.Apk;

        // Void Linux
        if (File.Exists("/usr/bin/xbps-install"))
            return LinuxPackageManager.Xbps;

        return LinuxPackageManager.Unknown;
    }

    /// <summary>
    /// Gets the Linux distribution information by reading the /etc/os-release file.
    /// </summary>
    /// <returns>A <see cref="Distribution"/> object containing the distribution information, or null if the information cannot be determined.</returns>
    private static Dictionary<string, string> GetOsRelease()
    {
        if (!OperatingSystem.IsLinux())
            return [];

        const string path = "/etc/os-release";

        if (!File.Exists(path))
            return [];

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var index = line.IndexOf('=');
            if (index <= 0)
                continue;

            var key = line[..index];
            var value = line[(index + 1)..].Trim();

            if (value is ['"', _, ..] &&
                value[^1] == '"')
            {
                value = value[1..^1]
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }

            values[key] = value;
        }

        return values;
    }

    private static LinuxDistribution GetLinuxDistribution()
    {
        var values = GetOsRelease();

        return new LinuxDistribution(
            values.GetValueOrDefault("ID"),
            values.GetValueOrDefault("NAME"),
            values.GetValueOrDefault("PRETTY_NAME"),
            values.GetValueOrDefault("VERSION_ID"),
            values.GetValueOrDefault("VERSION_CODENAME"),
            values.GetValueOrDefault("VERSION"),
            values.GetValueOrDefault("ID_LIKE")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [],
            values.GetValueOrDefault("HOME_URL"),
            values.GetValueOrDefault("SUPPORT_URL"),
            values.GetValueOrDefault("BUG_REPORT_URL"),
            values.GetValueOrDefault("PRIVACY_POLICY_URL"));
    }
}