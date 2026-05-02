namespace StageKit.Extensions;

/// <summary>
/// Provides extension methods for <see cref="DateTime"/> and <see cref="TimeSpan"/> types.
/// </summary>
public static class DateTimeExtensions
{
    /// <param name="dateTime">The DateTime</param>
    extension(DateTime dateTime)
    {
        /// <summary>
        /// Calculates the age, in years, based on the current UTC date.
        /// </summary>
        /// <returns>The age in years as an integer.</returns>
        public int Age()
        {
            return dateTime.Age(DateTime.UtcNow.Date);
        }

        /// <summary>
        /// Calculates the age in whole years as of the specified later date.
        /// </summary>
        /// <param name="laterDate">The date on which to calculate the age. Must be the same as or after the original date.</param>
        /// <returns>The number of complete years between the original date and <paramref name="laterDate"/>. Returns 0 if
        /// <paramref name="laterDate"/> is earlier than the original date.</returns>
        public int Age(DateTime laterDate)
        {
            var age = laterDate.Year - dateTime.Year;
            if (laterDate.Date < dateTime.Date.AddYears(age)) age--;
            return Math.Max(0, age);
        }
    }

    /// <summary>
    /// Converts a <see cref="TimeSpan"/> to a compact human-readable string (e.g. <c>"45s"</c>, <c>"02m30s"</c>, <c>"1day 03h00m00s"</c>).
    /// </summary>
    /// <param name="timeSpan">The duration to format. Negative values are formatted with a leading <c>"-"</c>.</param>
    /// <param name="showSeconds">When <see langword="true"/>, includes the seconds component.</param>
    /// <returns>A formatted duration string.</returns>
    public static string ToTimeString(this TimeSpan timeSpan, bool showSeconds = true)
    {
        if (timeSpan.Ticks < 0) return "-" + (-timeSpan).ToTimeString(showSeconds);

        var totalSeconds = timeSpan.TotalSeconds;
        var roundedSeconds = (long)Math.Round(totalSeconds, MidpointRounding.AwayFromZero);

        // Roll boundaries up after rounding (e.g. 59.6s -> 60s -> "01m00s").
        if (roundedSeconds != (long)totalSeconds)
        {
            timeSpan = TimeSpan.FromSeconds(roundedSeconds);
            totalSeconds = roundedSeconds;
        }

        return totalSeconds switch
        {
            < 60 => $"{roundedSeconds}s",
            < 3600 => showSeconds ? timeSpan.ToString(@"mm\mss\s") : timeSpan.ToString(@"mm\m"),
            < 86400 => showSeconds ? timeSpan.ToString(@"hh\hmm\mss\s") : timeSpan.ToString(@"hh\hmm\m"),
            < 172800 => showSeconds ? timeSpan.ToString(@"d'day 'hh\hmm\mss\s") : timeSpan.ToString(@"d'day 'hh\hmm\m"),
            _ => showSeconds ? timeSpan.ToString(@"d'days 'hh\hmm\mss\s") : timeSpan.ToString(@"d'days 'hh\hmm\m"),
        };
    }
}
