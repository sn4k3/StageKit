namespace StageKit;

internal static class Helpers
{
    /// <summary>
    /// Translate numeric file size in bytes to a human-readable shorter string format.
    /// </summary>
    /// <param name="size">File size in bytes.</param>
    /// <returns>Returns file size short string.</returns>
    internal static string ToFileSizeString(long size)
    {
        const long KB = 1024L;
        const long MB = KB * 1024;
        const long GB = MB * 1024;
        const long TB = GB * 1024;
        const long PB = TB * 1024;
        const long EB = PB * 1024;

        if (size < KB) return size + " bytes";
        if (size < MB) return (size / (float)KB).ToString("F1") + " KB";
        if (size < GB) return (size / (float)MB).ToString("F1") + " MB";
        if (size < TB) return (size / (float)GB).ToString("F1") + " GB";
        if (size < PB) return (size / (float)TB).ToString("F1") + " TB";
        if (size < EB) return (size / (float)PB).ToString("F1") + " PB";
        return (size / (float)EB).ToString("F1") + " EB";
    }
}