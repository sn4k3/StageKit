using System.IO;
using StageKit.Primitives.System;

namespace StageKit.Tests;

public sealed class HostSystemTests
{
    [Fact]
    public void Theme_PropertiesEvaluateWithoutThrowing()
    {
        _ = HostSystem.IsDarkMode;
        _ = HostSystem.IsHighContrast;
    }

    [Fact]
    public void Power_SnapshotAndSleepPrevention()
    {
        var status = new HostPowerStatus(
            hasBattery: true,
            isOnBatteryPower: false,
            batteryChargePercentage: 85,
            estimatedBatteryLife: TimeSpan.FromHours(4),
            isBatteryCharging: true);

        Assert.True(status.HasBattery);
        Assert.False(status.IsOnBatteryPower);
        Assert.Equal(85, status.BatteryChargePercentage);
        Assert.True(status.IsBatteryCharging);

        using (var token = HostSystem.PreventSleep(keepDisplayOn: false))
        {
            Assert.NotNull(token);
        }
    }

    [Fact]
    public void Disk_StatusAndFreeSpaceCalculations()
    {
        var status = new HostDiskStatus(
            driveName: "C:\\",
            totalSizeBytes: 1024L * 1024 * 1024 * 500,
            availableFreeSpaceBytes: 1024L * 1024 * 1024 * 200,
            totalFreeSpaceBytes: 1024L * 1024 * 1024 * 250,
            driveFormat: "NTFS",
            driveType: DriveType.Fixed);

        Assert.Equal(500.0, status.TotalSizeGigabytes, precision: 1);
        Assert.Equal(200.0, status.AvailableFreeSpaceGigabytes, precision: 1);

        var currentDir = Environment.CurrentDirectory;
        if (HostSystem.TryGetDiskStatus(currentDir, out var hostDisk))
        {
            Assert.NotNull(hostDisk);
            Assert.True(hostDisk.Value.TotalSizeBytes > 0);
            Assert.True(HostSystem.GetAvailableFreeSpace(currentDir) > 0);
        }

        Assert.ThrowsAny<ArgumentException>(() => HostSystem.GetDiskStatus("   "));
        Assert.False(HostSystem.TryGetAvailableFreeSpace(null!, out _));
    }

    [Fact]
    public void Shell_TerminalStartInfo()
    {
        var startInfo = HostSystem.CreateOpenTerminalStartInfo();
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.NotNull(startInfo);
            Assert.False(string.IsNullOrWhiteSpace(startInfo.FileName));
        }
        else if (startInfo is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(startInfo.FileName));
        }

        var invalidDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Null(HostSystem.CreateOpenTerminalStartInfo(invalidDir));

        var cmdStartInfo = HostSystem.CreateOpenInTerminalStartInfo("echo hello", keepOpen: false);
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.NotNull(cmdStartInfo);
            Assert.False(string.IsNullOrWhiteSpace(cmdStartInfo.FileName));
        }
        else if (cmdStartInfo is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(cmdStartInfo.FileName));
        }

        Assert.Null(HostSystem.CreateOpenInTerminalStartInfo("   "));
    }
}
