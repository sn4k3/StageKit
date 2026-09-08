using System.Globalization;

namespace StageKit.Demo;

public static class DemoFormatting
{
    private static readonly string[] ByteSizeUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];

    public static string FormatByteSize(ulong bytes)
    {
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < ByteSizeUnits.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes} {ByteSizeUnits[unitIndex]}")
            : string.Create(CultureInfo.InvariantCulture, $"{value:F2} {ByteSizeUnits[unitIndex]}");
    }

    public static string FormatDownloadProgress(
        double downloadedMegabytes,
        double totalMegabytes,
        double percentage)
    {
        var boundedPercentage = Math.Clamp(percentage, 0, 100);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{downloadedMegabytes:F2} MB / {totalMegabytes:F2} MB ({boundedPercentage:F0}%)");
    }
}
