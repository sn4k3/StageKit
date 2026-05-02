using System.Text;
using StageKit.Extensions;

namespace StageKit;

public static partial class ApplicationKit
{
    /// <summary>
    /// Gets or sets the date and time of birth for this application. This value is used to calculate the age of the application.
    /// </summary>
    /// <example><code>DateTime.SpecifyKind(new(2025, 7, 1, 2, 00, 00), DateTimeKind.Utc);</code></example>
    public static DateTime Birth { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Return full age in years
    /// </summary>
    public static int YearsOld => Birth.Age();

    /// <summary>
    /// Return full age in a readable string including hours, minutes, and seconds.
    /// </summary>
    public static string AgeStr => BuildAgeString(includeTime: true);

    /// <summary>
    /// Return full age in a readable string limited to years, months, and days.
    /// </summary>
    public static string AgeShortStr => BuildAgeString(includeTime: false);

    /// <summary>
    /// Checks if today is the application's birthday. For a Feb 29 birthdate in a non-leap year the anniversary folds to Feb 28.
    /// </summary>
    public static bool IsBirthday
    {
        get
        {
            var today = DateTime.UtcNow.Date;
            return AnniversaryFor(Birth, today.Year) == today;
        }
    }

    /// <summary>
    /// Checks if today is within a week after the application's birthday anniversary.
    /// </summary>
    public static bool IsBirthdayWithinAWeek => IsBirthdayWithOffset(7);

    /// <summary>
    /// Checks if today is within <paramref name="daysOffset"/> days after the most recent birthday anniversary.
    /// Cross-month and cross-year windows are handled correctly.
    /// </summary>
    /// <param name="daysOffset">Number of positive days after the birthday still considered as birthday window.</param>
    /// <returns><see langword="true"/> when today is within the window; otherwise <see langword="false"/>.</returns>
    public static bool IsBirthdayWithOffset(int daysOffset)
    {
        if (daysOffset < 0) return false;

        var today = DateTime.UtcNow.Date;
        if (Birth.Date > today) return false;

        var anniversary = AnniversaryFor(Birth, today.Year);
        if (anniversary > today) anniversary = AnniversaryFor(Birth, today.Year - 1);

        var diff = (today - anniversary).Days;
        return diff >= 0 && diff <= daysOffset;
    }

    /// <summary>
    /// Calculates the anniversary date for a given year, folding Feb 29 to Feb 28 in non-leap years.
    /// </summary>
    /// <param name="born">The original birth date.</param>
    /// <param name="year">The year for which to calculate the anniversary.</param>
    /// <returns>The anniversary date for the specified year.</returns>
    private static DateTime AnniversaryFor(DateTime born, int year)
    {
        var day = Math.Min(born.Day, DateTime.DaysInMonth(year, born.Month));
        return new DateTime(year, born.Month, day, 0, 0, 0, born.Kind);
    }

    /// <summary>
    /// Builds a human-readable string representing the elapsed time since the date of birth, expressed in years,
    /// months, days, and optionally hours, minutes, and seconds.
    /// </summary>
    /// <remarks>The returned string always includes years, even if zero. If includeTime is true, the string
    /// will also include hours, minutes, and seconds components.</remarks>
    /// <param name="includeTime">true to include hours, minutes, and seconds in the output; otherwise, false to include only years, months, and
    /// days.</param>
    /// <returns>A string that describes the elapsed time since the date of birth in years, months, and days, and optionally
    /// hours, minutes, and seconds. Returns "0 years" if the current time is before the date of birth.</returns>
    private static string BuildAgeString(bool includeTime)
    {
        var born = Birth;
        var now = DateTime.UtcNow;
        if (now < born) return "0 years";

        var years = born.Age(now);
        var cursor = born.AddYears(years);

        var months = 0;
        while (cursor.AddMonths(1) <= now)
        {
            cursor = cursor.AddMonths(1);
            months++;
        }

        var remainder = now - cursor;
        var sb = new StringBuilder();
        AppendUnit(sb, years, "year", forceShow: true);
        AppendUnit(sb, months, "month");
        AppendUnit(sb, remainder.Days, "day");
        if (includeTime)
        {
            AppendUnit(sb, remainder.Hours, "hour");
            AppendUnit(sb, remainder.Minutes, "minute");
            AppendUnit(sb, remainder.Seconds, "second");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Appends a formatted unit string to the specified StringBuilder, including the count and the singular or plural
    /// form of the unit name.
    /// </summary>
    /// <remarks>If the StringBuilder already contains content, a comma and space are added before the unit
    /// string. This method does not handle irregular plural forms.</remarks>
    /// <param name="sb">The StringBuilder to which the formatted unit string is appended.</param>
    /// <param name="count">The numeric value representing the quantity of the unit. If zero and forceShow is false, nothing is appended.</param>
    /// <param name="singular">The singular form of the unit name to append. An 's' is added for pluralization if count is greater than one.</param>
    /// <param name="forceShow">true to append the unit string even if count is zero; otherwise, false.</param>
    private static void AppendUnit(StringBuilder sb, int count, string singular, bool forceShow = false)
    {
        if (count == 0 && !forceShow) return;
        if (sb.Length > 0) sb.Append(", ");
        sb.Append(count)
            .Append(' ')
            .Append(singular);
        if (count > 1) sb.Append('s');
    }
}
