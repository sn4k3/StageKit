using System.Globalization;

namespace StageKit.Demo;

public static class DemoFormatting
{
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
