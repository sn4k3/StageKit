using StageKit.Primitives.System;

namespace StageKit.Tests;

public sealed class HostSystemMemoryTests
{
    [Fact]
    public void CurrentHost_ReturnsUsableSnapshot()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.False(HostSystem.TryGetMemoryStatus(out _));
            return;
        }

        Assert.True(HostSystem.TryGetMemoryStatus(out var status));
        Assert.True(status.TotalPhysicalBytes > 0);
        Assert.InRange(status.AvailablePhysicalBytes, 0UL, status.TotalPhysicalBytes);
        Assert.Equal(status.TotalPhysicalBytes, status.UsedPhysicalBytes + status.AvailablePhysicalBytes);
        Assert.InRange(status.MemoryLoadPercentage, 0d, 100d);
        Assert.True(HostSystem.GetMemoryStatus().TotalPhysicalBytes > 0);
    }

    [Theory]
    [InlineData("MemFree: 100 kB\nMemAvailable: 600 kB", 600UL)]
    [InlineData("MemAvailable: 600 kB\nMemFree: 100 kB", 600UL)]
    [InlineData("MemFree: 100 kB", 100UL)]
    [InlineData("MemAvailable: 0 kB\nMemFree: 100 kB", 0UL)]
    [InlineData("MemAvailable: 2000 kB", 1000UL)]
    public void Linux_UsesAvailableMemoryWithFreeFallback(string entries, ulong available)
    {
        Assert.True(HostSystem.TryParseLinuxMemoryStatus(
            "MemTotal: 1000 kB\r\nHugePages_Total: 0\n" + entries, out var status));
        Assert.Equal(1000UL * 1024, status.TotalPhysicalBytes);
        Assert.Equal(available * 1024, status.AvailablePhysicalBytes);
        Assert.Equal((1000UL - available) * 1024, status.UsedPhysicalBytes);
        Assert.Equal(100 - available / 10d, status.MemoryLoadPercentage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MemTotal: 100 kB")]
    [InlineData("MemTotal: 0 kB\nMemAvailable: 0 kB")]
    [InlineData("MemTotal: invalid kB\nMemAvailable: 1 kB")]
    [InlineData("MemTotal: 18446744073709551615 kB\nMemAvailable: 1 kB")]
    [InlineData("MemTotal: 100 MB\nMemAvailable: 1 kB")]
    [InlineData("MemTotal: 100 kB\nMemAvailable: -1 kB")]
    public void Linux_RejectsIncompleteOrMalformedSnapshots(string text)
    {
        Assert.False(HostSystem.TryParseLinuxMemoryStatus(text, out var status));
        Assert.Equal(0UL, status.TotalPhysicalBytes);
        Assert.Equal(0d, status.MemoryLoadPercentage);
    }
}
