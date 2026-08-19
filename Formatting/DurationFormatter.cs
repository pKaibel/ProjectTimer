using System.Globalization;

namespace ProjectTimer.Formatting;

public static class DurationFormatter
{
    public static string Format(TimeSpan duration)
    {
        var safeTicks = Math.Max(0, duration.Ticks);
        var totalMinutes = (long)Math.Floor(TimeSpan.FromTicks(safeTicks).TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours == 0)
        {
            return $"{minutes.ToString(CultureInfo.CurrentCulture)} min";
        }

        return $"{hours.ToString(CultureInfo.CurrentCulture)} h {minutes:00} min";
    }

    public static string FormatLong(TimeSpan duration)
    {
        var safeTicks = Math.Max(0, duration.Ticks);
        var totalMinutes = (long)Math.Floor(TimeSpan.FromTicks(safeTicks).TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        var hourLabel = hours == 1 ? "Stunde" : "Stunden";
        var minuteLabel = minutes == 1 ? "Minute" : "Minuten";
        return $"{hours} {hourLabel} {minutes} {minuteLabel}";
    }

    public static string FormatTimer(TimeSpan duration)
    {
        var safe = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        var totalHours = (long)Math.Floor(safe.TotalHours);
        return $"{totalHours:00}:{safe.Minutes:00}:{safe.Seconds:00}";
    }
}
