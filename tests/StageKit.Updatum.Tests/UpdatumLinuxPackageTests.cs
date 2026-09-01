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
}