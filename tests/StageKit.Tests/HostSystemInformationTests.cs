using StageKit.Primitives.System;

namespace StageKit.Tests;

public sealed class HostSystemInformationTests
{
    [Fact]
    public void LinuxProcessorParser_PrefersSpecificModelName()
    {
        const string cpuInfo = """
                               processor : 0
                               Hardware  : Generic ARM platform
                               Processor : ARMv8 Processor rev 1 (v8l)
                               model name: Example CPU 9000
                               """;

        Assert.Equal("Example CPU 9000", HostSystem.ParseLinuxProcessorName(cpuInfo));
    }

    [Theory]
    [InlineData("processor : 0\nHardware : BCM2711", "BCM2711")]
    [InlineData("processor : AArch64 Processor rev 4", "AArch64 Processor rev 4")]
    [InlineData("cpu model : POWER9, altivec supported", "POWER9, altivec supported")]
    [InlineData("processor : 0\nprocessor : 1", null)]
    public void LinuxProcessorParser_HandlesArchitectureVariants(string cpuInfo, string? expected)
    {
        Assert.Equal(expected, HostSystem.ParseLinuxProcessorName(cpuInfo));
    }

    [Fact]
    public void LspciParser_ReturnsEveryGraphicsControllerAndRemovesNumericIdentifiers()
    {
        const string output = """
                              0000:01:00.0 "VGA compatible controller [0300]" "NVIDIA Corporation [10de]" "AD104 [GeForce RTX 4070] [2786]"
                              0000:02:00.0 "3D controller [0302]" "Advanced Micro Devices, Inc. [AMD/ATI] [1002]" "Navi 33 [Radeon RX 7600] [7480]"
                              0000:03:00.0 "Display controller [0380]" "Example Vendor [1234]" "Virtual Display [5678]"
                              0000:04:00.0 "Ethernet controller [0200]" "Network Vendor [1111]" "Network Device [2222]"
                              0000:05:00.0 "VGA compatible controller [0300]" "nvidia corporation [10de]" "AD104 [GeForce RTX 4070] [2786]"
                              """;

        var names = HostSystem.ParseLspciGraphicsCardNames(output);

        Assert.Equal(3, names.Count);
        Assert.Equal("NVIDIA Corporation AD104 [GeForce RTX 4070]", names[0]);
        Assert.Equal("Advanced Micro Devices, Inc. [AMD/ATI] Navi 33 [Radeon RX 7600]", names[1]);
        Assert.Equal("Example Vendor Virtual Display", names[2]);
    }

    [Fact]
    public void MacSystemProfilerParser_ReturnsModelAndNameFallbacks()
    {
        const string output = """
                              {
                                "SPDisplaysDataType": [
                                  { "_name": "Fallback name", "sppci_model": "Apple M4 Max" },
                                  { "_name": "External Display Adapter" },
                                  { "_name": "apple m4 max" }
                                ]
                              }
                              """;

        var names = HostSystem.ParseMacGraphicsCardNames(output);

        Assert.Equal(2, names.Count);
        Assert.Equal("Apple M4 Max", names[0]);
        Assert.Equal("External Display Adapter", names[1]);
    }

    [Theory]
    [InlineData(@"pci\ven_10de&dev_2684", true)]
    [InlineData(@"ACPI\VEN_QCOM&DEV_043A", true)]
    [InlineData("usbmmidd", false)]
    [InlineData(@"USB\VID_17E9&PID_6006", false)]
    [InlineData(@"ROOT\INDIRECTDISPLAY", false)]
    [InlineData(null, false)]
    public void WindowsGraphicsAdapterFilter_ReturnsOnlyHardwareDeviceIdentifiers(
        string? matchingDeviceId,
        bool expected)
    {
        Assert.Equal(expected, HostSystem.IsWindowsHardwareGraphicsAdapter(matchingDeviceId));
    }
}
