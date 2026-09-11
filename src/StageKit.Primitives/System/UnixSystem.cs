using System.Diagnostics;
using System.Runtime.Versioning;

namespace StageKit.Primitives.System;

/// <summary>
/// Provides Unix-specific system utilities.
/// </summary>
public static class UnixSystem
{
    /// <summary>
    /// Marks a Unix launcher as executable.
    /// </summary>
    /// <param name="executablePath">The launcher path.</param>
    /// <remarks>
    /// Windows has no Unix mode bits, so the call is a no-op there. Bundle creation that depends on the
    /// execute bit is already gated on a matching host, and a cross-built payload staged on Windows simply
    /// carries no mode to set.
    /// </remarks>
    public static void SetUnix755Executable(string executablePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(executablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    /// <summary>
    /// Sets Unix file permissions.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="value">The permission value (e.g., "755", "644", "u+rwx").</param>
    /// <param name="currentMode">The current Unix file mode.</param>
    public static void SetUnixPermissions(string filePath, string value, UnixFileMode currentMode = UnixFileMode.None)
    {
        if (OperatingSystem.IsWindows())
            return;

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        value = value.Trim();

        // Octal: 755, 644, 0755, 4755, etc.
        if (value.Length is 3 or 4 && value.All(c => c is >= '0' and <= '7'))
        {
            File.SetUnixFileMode(filePath, (UnixFileMode)Convert.ToInt32(value, 8));
            return;
        }

        var operationIndex = value.IndexOfAny(['+', '-', '=']);
        if (operationIndex < 0 || operationIndex == value.Length - 1)
            throw new FormatException($"Invalid chmod mode: '{value}'.");

        var operation = value[operationIndex];
        var who = value.AsSpan(0, operationIndex);
        var permissions = value.AsSpan(operationIndex + 1);

        // No "who" means all: +x => a+x
        var user = who.IsEmpty || who.Contains('a') || who.Contains('u');
        var group = who.IsEmpty || who.Contains('a') || who.Contains('g');
        var other = who.IsEmpty || who.Contains('a') || who.Contains('o');

        var mask = UnixFileMode.None;

        foreach (var permission in permissions)
        {
            mask |= permission switch
            {
                'r' => (user ? UnixFileMode.UserRead : 0) |
                       (group ? UnixFileMode.GroupRead : 0) |
                       (other ? UnixFileMode.OtherRead : 0),

                'w' => (user ? UnixFileMode.UserWrite : 0) |
                       (group ? UnixFileMode.GroupWrite : 0) |
                       (other ? UnixFileMode.OtherWrite : 0),

                'x' => (user ? UnixFileMode.UserExecute : 0) |
                       (group ? UnixFileMode.GroupExecute : 0) |
                       (other ? UnixFileMode.OtherExecute : 0),

                _ => throw new FormatException($"Unsupported chmod permission: '{permission}'.")
            };
        }

        if (operation == '=')
        {
            var clear = UnixFileMode.None;

            if (user)
                clear |= UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

            if (group)
                clear |= UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute;

            if (other)
                clear |= UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

            currentMode &= ~clear;
        }

        var result = operation switch
        {
            '+' => currentMode | mask,
            '-' => currentMode & ~mask,
            '=' => currentMode | mask,
            _ => throw new UnreachableException()
        };

        File.SetUnixFileMode(filePath, result);
    }

    /// <summary>
    /// Changes the Unix file permissions for the specified file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="value">The permission value (e.g., "755", "644", "u+rwx").</param>
    /// <exception cref="ArgumentException"></exception>
    /// <remarks>This method is a wrapper around the SetUnixPermissions method which gets the current Unix file mode before applying the changes.</remarks>
    public static void ChangeUnixPermissions(string filePath, string value)
    {
        if (OperatingSystem.IsWindows())
            return;
        
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        
        SetUnixPermissions(filePath, value, File.GetUnixFileMode(filePath));
    }
}