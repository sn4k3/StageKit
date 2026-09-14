using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
    private const string WindowsPersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string WindowsAccessibilityRegistryPath =
        @"Control Panel\Accessibility\HighContrast";

    private const int SpiGetHighContrast = 0x0042;
    private const int HcfHighContrastOn = 0x00000001;
    
    /// <summary>
    /// Gets a value indicating whether the operating system is currently using a light theme.
    /// </summary>
    /// <remarks>
    /// On Windows, queries the user's personalization registry settings. On macOS, queries AppleInterfaceStyle.
    /// On Linux, queries desktop color-scheme preferences via gsettings and environment variables.
    /// Returns <see langword="false"/> if theme preference cannot be determined.
    /// </remarks>
    public static bool IsLightMode => !IsDarkMode;

    /// <summary>
    /// Gets a value indicating whether the operating system is currently using a dark theme.
    /// </summary>
    /// <remarks>
    /// On Windows, queries the user's personalization registry settings. On macOS, queries AppleInterfaceStyle.
    /// On Linux, queries desktop color-scheme preferences via gsettings and environment variables.
    /// Returns <see langword="false"/> if theme preference cannot be determined.
    /// </remarks>
    public static bool IsDarkMode => GetIsDarkMode();

    /// <summary>
    /// Gets a value indicating whether high contrast mode is currently enabled on the host.
    /// </summary>
    /// <remarks>
    /// On Windows, queries SystemParametersInfo and accessibility settings. On macOS, queries universal access contrast settings.
    /// On Linux, queries desktop accessibility preferences and GTK theme settings.
    /// Returns <see langword="false"/> if high contrast mode cannot be determined.
    /// </remarks>
    public static bool IsHighContrast => GetIsHighContrast();

    private static bool GetIsDarkMode()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return GetWindowsIsDarkMode();

            if (OperatingSystem.IsMacOS())
                return GetMacIsDarkMode();

            if (OperatingSystem.IsLinux())
                return GetLinuxIsDarkMode();
        }
        catch
        {
            // Best effort
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool GetWindowsIsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsPersonalizeRegistryPath);
            if (key is not null)
            {
                if (key.GetValue("AppsUseLightTheme") is int appsValue)
                    return appsValue == 0;

                if (key.GetValue("SystemUsesLightTheme") is int systemValue)
                    return systemValue == 0;
            }
        }
        catch
        {
            // Best effort
        }

        return false;
    }

    private static bool GetMacIsDarkMode()
    {
        var output = GetBoundedProcessOutput("/usr/bin/defaults", ["read", "-g", "AppleInterfaceStyle"]);
        return string.Equals(output, "Dark", StringComparison.OrdinalIgnoreCase);
    }

    private static bool GetLinuxIsDarkMode()
    {
        var gtkTheme = Environment.GetEnvironmentVariable("GTK_THEME");
        if (!string.IsNullOrEmpty(gtkTheme) && gtkTheme.Contains("dark", StringComparison.OrdinalIgnoreCase))
            return true;

        var colorScheme = GetBoundedProcessOutput("gsettings", ["get", "org.gnome.desktop.interface", "color-scheme"]);
        if (colorScheme is not null && colorScheme.Contains("prefer-dark", StringComparison.OrdinalIgnoreCase))
            return true;

        var theme = GetBoundedProcessOutput("gsettings", ["get", "org.gnome.desktop.interface", "gtk-theme"]);
        return theme is not null && theme.Contains("dark", StringComparison.OrdinalIgnoreCase);
    }

    private static bool GetIsHighContrast()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return GetWindowsIsHighContrast();

            if (OperatingSystem.IsMacOS())
                return GetMacIsHighContrast();

            if (OperatingSystem.IsLinux())
                return GetLinuxIsHighContrast();
        }
        catch
        {
            // Best effort
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool GetWindowsIsHighContrast()
    {
        try
        {
            var highContrast = new HighContrastData
            {
                cbSize = Marshal.SizeOf<HighContrastData>()
            };

            if (SystemParametersInfo(SpiGetHighContrast, highContrast.cbSize, ref highContrast, 0) &&
                (highContrast.dwFlags & HcfHighContrastOn) != 0)
            {
                return true;
            }
        }
        catch
        {
            // Fallback to registry
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsAccessibilityRegistryPath);
            if (key?.GetValue("Flags") is string flagsStr && int.TryParse(flagsStr, out var flags))
                return (flags & HcfHighContrastOn) != 0;
        }
        catch
        {
            // Best effort
        }

        return false;
    }

    private static bool GetMacIsHighContrast()
    {
        var output = GetBoundedProcessOutput("/usr/bin/defaults", ["read", "com.apple.universalaccess", "increaseContrast"]);
        return string.Equals(output, "1", StringComparison.Ordinal);
    }

    private static bool GetLinuxIsHighContrast()
    {
        var gtkTheme = Environment.GetEnvironmentVariable("GTK_THEME");
        if (!string.IsNullOrEmpty(gtkTheme) && gtkTheme.Contains("HighContrast", StringComparison.OrdinalIgnoreCase))
            return true;

        var highContrast = GetBoundedProcessOutput("gsettings", ["get", "org.gnome.desktop.interface", "high-contrast"]);
        return string.Equals(highContrast, "true", StringComparison.OrdinalIgnoreCase);
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(
        int uiAction,
        int uiParam,
        ref HighContrastData pvParam,
        int fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrastData
    {
        public int cbSize;
        public int dwFlags;
        public IntPtr lpszDefaultScheme;
    }
}
