namespace StageKit.Primitives.Extensions;

/// <summary>
/// 
/// </summary>
public class ConverterExtension
{
    /// <summary>
    /// Translate numeric file size in bytes to a human-readable shorter string format.
    /// </summary>
    /// <param name="size">File size in bytes.</param>
    /// <param name="roundToDecimals">Round to this number of decimal places.</param>
    /// <returns>Returns file size short string.</returns>
    public static string ToFileSizeString(long size, int roundToDecimals = 2)
    {
        if (size < 0) return string.Empty;
        else if (size < 1024)
        {
            return $"{size:F0} bytes";
        }
        else if (size >> 10 < 1024)
        {
            return $"{Math.Round(size / 1024F, roundToDecimals, MidpointRounding.AwayFromZero)} KB";
        }
        else if (size >> 20 < 1024)
        {
            return $"{Math.Round((size >> 10) / 1024F, roundToDecimals, MidpointRounding.AwayFromZero)} MB";
        }
        else if (size >> 30 < 1024)
        {
            return $"{Math.Round((size >> 20) / 1024F, roundToDecimals, MidpointRounding.AwayFromZero)} GB";
        }
        else if (size >> 40 < 1024)
        {
            return $"{Math.Round((size >> 30) / 1024F, roundToDecimals, MidpointRounding.AwayFromZero)} TB";
        }
        else if (size >> 50 < 1024)
        {
            return $"{Math.Round((size >> 40) / 1024F, roundToDecimals, MidpointRounding.AwayFromZero)} PB";
        }
        else
        {
            return $"{Math.Round((size >> 50) / 1024F, roundToDecimals, MidpointRounding.AwayFromZero)} EB";
        }
    }

    /// <summary>
    /// Translate numeric internet speed in bits to a human-readable shorter string format.
    /// </summary>
    /// <param name="speed">Internet speed in bits.</param>
    /// <returns>Returns internet speed short string.</returns>
    public static string ToInternetSpeedString(long speed)
    {
        if (speed < 0) return string.Empty;
        else if (speed < 1000)
        {
            return $"{speed:F0} bits";
        }
        else if (speed < 1000_000)
        {
            return $"{speed / 1000F:F1} Kbps";
        }
        else if (speed < 1000_000_000)
        {
            return $"{speed / 1000_000F:F1} Mbps";
        }
        else if (speed < 1000_000_000_000)
        {
            return $"{speed / 1000_000_000F:F1} Gbps";
        }
        else if (speed < 1000_000_000_000_000)
        {
            return $"{speed / 1000_000_000_000F:F1} Tbps";
        }
        else if (speed < 1000_000_000_000_000_000)
        {
            return $"{speed / 1000_000_000_000_000F:F1} Pbps";
        }
        else
        {
            return $"{speed / 1000_000_000_000_000_000F:F1} Ebps";
        }
    }

    /// <summary>
    /// Translate numeric time in seconds to a human-readable shorter string format.
    /// </summary>
    /// <param name="seconds">Time in seconds.</param>
    /// <returns>Returns time short string.</returns>
    public static string ToTimeShortString(long seconds)
    {
        if (seconds < 0) return string.Empty;
        else if (seconds < 60)
        {
            return $"{seconds} s";
        }
        else if (seconds / 60 < 60)
        {
            return $"{seconds / 60F:#.#} m";
        }
        else if (seconds / 3600 < 24)
        {
            return $"{seconds / 60.0 / 60F:#.#} h";
        }
        else if (seconds / 86400 < 365)
        {
            return $"{seconds / 60.0 / 60 / 24F:#.#} d";
        }
        else
        {
            return $"{seconds / 60.0 / 60 / 24 / 365F:#.#} y";
        }
    }
}