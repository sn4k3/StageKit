using System.Runtime.InteropServices;
using StageKit.Primitives.System;
using StageKit.Runtime;
using StageKit.Runtime.System;

namespace StageKit.Updatum.Tests;

public sealed class UpdatumLinuxPackageTests
{
    [Theory]
    [InlineData("update.deb", LinuxPackageManager.Apt, "apt-get", true)]
    [InlineData("update.rpm", LinuxPackageManager.Dnf, "dnf", true)]
    [InlineData("update.rpm", LinuxPackageManager.Zypper, "zypper", true)]
    [InlineData("update.pkg.tar.zst", LinuxPackageManager.Pacman, "pacman", true)]
    [InlineData("update.snap", LinuxPackageManager.Unknown, "snap", true)]
    public void CreateLinuxPackageInstallCommand_UsesNativeElevatedInstaller(
        string filePath,
        LinuxPackageManager packageManager,
        string expectedExecutable,
        bool expectedElevation)
    {
        var command = UpdatumManager.CreateLinuxPackageInstallCommand(
            filePath,
            packageManager,
            UpdatumManager.FlatpakInstallationScope.User);

        Assert.Equal(expectedExecutable, command.Executable);
        Assert.Equal(expectedElevation, command.RequiresElevation);
        Assert.Equal(filePath, command.Arguments[^1]);
    }

    [Theory]
    [InlineData(0, false, "--user")]
    [InlineData(1, true, "--system")]
    public void CreateLinuxPackageInstallCommand_MatchesFlatpakInstallationScope(
        int scopeValue,
        bool expectedElevation,
        string expectedScopeArgument)
    {
        var scope = (UpdatumManager.FlatpakInstallationScope)scopeValue;
        var command = UpdatumManager.CreateLinuxPackageInstallCommand(
            "update.flatpak",
            LinuxPackageManager.Unknown,
            scope);

        Assert.Equal("flatpak", command.Executable);
        Assert.Equal(expectedElevation, command.RequiresElevation);
        Assert.Equal(expectedScopeArgument, command.Arguments[0]);
        Assert.Equal([expectedScopeArgument, "install", "--or-update", "--noninteractive", "update.flatpak"],
            command.Arguments);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(126)]
    public void EnsurePackageInstallationSucceeded_RejectsFailedOrCancelledElevation(int exitCode)
    {
        var exception = Assert.Throws<IOException>(() =>
            UpdatumManager.EnsurePackageInstallationSucceeded("Debian", exitCode));

        Assert.Contains(exitCode.ToString(), exception.Message);
    }

    [Fact]
    public void EnsurePackageInstallationSucceeded_AcceptsZeroExitCode()
    {
        UpdatumManager.EnsurePackageInstallationSucceeded("Debian", 0);
    }

    [Fact]
    public void EnsurePackageInstallationSucceeded_ReportsDeniedElevationDistinctly()
    {
        var deniedExitCode = OperatingSystem.IsWindows()
            ? ProcessHelper.WindowsElevationCancelledExitCode
            : ProcessHelper.LinuxElevationDismissedExitCode;

        // macOS cannot classify a denial from the exit code alone, so it keeps the generic message.
        var expectsDenialMessage = !OperatingSystem.IsMacOS();

        var exception = Assert.Throws<IOException>(() =>
            UpdatumManager.EnsurePackageInstallationSucceeded("Debian", deniedExitCode));

        Assert.Equal(expectsDenialMessage, exception.Message.Contains("administrator privileges"));
        Assert.Contains(deniedExitCode.ToString(), exception.Message);
    }

    [Theory]
    [InlineData("update.deb", ApplicationPackagingType.LinuxDeb)]
    [InlineData("update.rpm", ApplicationPackagingType.LinuxRpm)]
    [InlineData("update.snap", ApplicationPackagingType.LinuxSnap)]
    [InlineData("update.flatpak", ApplicationPackagingType.LinuxFlatpak)]
    [InlineData("update.AppImage", ApplicationPackagingType.LinuxAppImage)]
    // The Arch suffix must win over the macOS ".pkg" it contains.
    [InlineData("update.pkg.tar.zst", ApplicationPackagingType.LinuxArchPackage)]
    public void GetPackagingTypeForFile_ResolvesLinuxPackagesForTheLinuxTarget(
        string filePath,
        ApplicationPackagingType expected)
    {
        Assert.Equal(expected, UpdatumManager.GetPackagingTypeForFile(filePath, OSPlatform.Linux));
    }

    [Theory]
    [InlineData("update.pkg", ApplicationPackagingType.MacOSPkg)]
    [InlineData("update.dmg", ApplicationPackagingType.MacOSDmg)]
    public void GetPackagingTypeForFile_ResolvesMacOSPackagesForTheMacOSTarget(
        string filePath,
        ApplicationPackagingType expected)
    {
        Assert.Equal(expected, UpdatumManager.GetPackagingTypeForFile(filePath, OSPlatform.OSX));
    }

    [Theory]
    [InlineData("update.pkg")]
    [InlineData("update.dmg")]
    public void GetPackagingTypeForFile_RejectsForeignPlatformPackages(string filePath)
    {
        Assert.Null(UpdatumManager.GetPackagingTypeForFile(filePath, OSPlatform.Linux));
    }

    [Theory]
    // A platform-agnostic type still resolves under an explicit target.
    [InlineData("update.zip", OperatingSystemTarget.Linux, ApplicationPackagingType.Portable)]
    [InlineData("update.zip", OperatingSystemTarget.Windows, ApplicationPackagingType.Portable)]
    public void GetPackagingTypeForFile_KeepsPlatformAgnosticTypes(
        string filePath,
        OperatingSystemTarget target,
        ApplicationPackagingType expected)
    {
        var platform = target is OperatingSystemTarget.Linux ? OSPlatform.Linux : OSPlatform.Windows;

        Assert.Equal(expected, UpdatumManager.GetPackagingTypeForFile(filePath, platform));
    }

    [Fact]
    public void GetPackagingTypeForFile_ReturnsNullForUnknownExtensions()
    {
        Assert.Null(UpdatumManager.GetPackagingTypeForFile("update.unknownext", OSPlatform.Linux));
    }

    /// <summary>
    /// Identifies a target platform for theory data, because <see cref="OSPlatform"/> is not a constant.
    /// </summary>
    public enum OperatingSystemTarget
    {
        /// <summary>Targets Windows.</summary>
        Windows,

        /// <summary>Targets Linux.</summary>
        Linux
    }
}