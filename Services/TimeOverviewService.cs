using System.Globalization;
using ProjectTimer.Formatting;
using ProjectTimer.Models;

namespace ProjectTimer.Services;

public enum OverviewPeriod
{
    Day,
    Week,
    Month,
    Year
}

public sealed class TimeOverviewService
{
    private const double MaximumBarWidth = 180;
    private readonly DatabaseService _database;

    public TimeOverviewService(DatabaseService database)
    {
        _database = database;
    }

    public async Task<TimeOverview> GetOverviewAsync(OverviewPeriod period)
    {
        var today = DateTime.Today;
        var buckets = CreateBuckets(period, today);
        var entries = await _database.GetAllTimeEntriesAsync();
        var runningTimer = await _database.GetActiveTimerAsync();
        if (runningTimer is not null)
        {
            entries.Add(new TimeEntry
            {
                ProjectId = runningTimer.ProjectId,
                StartAtUtc = runningTimer.StartedAtUtc,
                EndAtUtc = DateTime.UtcNow
            });
        }

        foreach (var entry in entries)
        {
            foreach (var bucket in buckets)
            {
                bucket.Duration += GetOverlap(entry, bucket.StartLocal, bucket.EndLocal);
            }
        }

        var maximum = buckets.Max(bucket => bucket.Duration.Ticks);
        var bars = buckets.Select(bucket => new TimeChartBar(
            bucket.Label,
            DurationFormatter.Format(bucket.Duration),
            maximum == 0 ? 0 : Math.Max(6, MaximumBarWidth * bucket.Duration.Ticks / maximum))).ToList();
        var total = TimeSpan.FromTicks(buckets.Sum(bucket => bucket.Duration.Ticks));
        return new TimeOverview(GetTitle(period, today), GetSubtitle(period, today), DurationFormatter.FormatLong(total), bars);
    }

    private static List<Bucket> CreateBuckets(OverviewPeriod period, DateTime today)
    {
        return period switch
        {
            OverviewPeriod.Day => Enumerable.Range(0, 24)
                .Select(hour => new Bucket(today.AddHours(hour), today.AddHours(hour + 1), $"{hour:00} Uhr"))
                .ToList(),
            OverviewPeriod.Week => CreateDateBuckets(GetWeekStart(today), 7, date => date.ToString("ddd", CultureInfo.CurrentCulture)),
            OverviewPeriod.Month => CreateDateBuckets(new DateTime(today.Year, today.Month, 1), DateTime.DaysInMonth(today.Year, today.Month), date => date.Day.ToString("00", CultureInfo.CurrentCulture)),
            OverviewPeriod.Year => Enumerable.Range(1, 12)
                .Select(month => new Bucket(new DateTime(today.Year, month, 1), new DateTime(today.Year, month, 1).AddMonths(1), new DateTime(today.Year, month, 1).ToString("MMM", CultureInfo.CurrentCulture)))
                .ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    private static List<Bucket> CreateDateBuckets(DateTime firstDate, int count, Func<DateTime, string> label)
    {
        return Enumerable.Range(0, count)
            .Select(offset => firstDate.AddDays(offset))
            .Select(date => new Bucket(date, date.AddDays(1), label(date)))
            .ToList();
    }

    private static TimeSpan GetOverlap(TimeEntry entry, DateTime startLocal, DateTime endLocal)
    {
        var startUtc = ToUtc(startLocal);
        var endUtc = ToUtc(endLocal);
        var overlapStart = entry.StartAtUtc > startUtc ? entry.StartAtUtc : startUtc;
        var overlapEnd = entry.EndAtUtc < endUtc ? entry.EndAtUtc : endUtc;
        return overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
    }

    private static DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZoneInfo.Local);
    }

    private static DateTime GetWeekStart(DateTime day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-offset).Date;
    }

    private static string GetTitle(OverviewPeriod period, DateTime today) => period switch
    {
        OverviewPeriod.Day => "Heute",
        OverviewPeriod.Week => "Diese Woche",
        OverviewPeriod.Month => "Dieser Monat",
        OverviewPeriod.Year => "Dieses Jahr",
        _ => string.Empty
    };

    private static string GetSubtitle(OverviewPeriod period, DateTime today) => period switch
    {
        OverviewPeriod.Day => today.ToString("dddd, d. MMMM", CultureInfo.CurrentCulture),
        OverviewPeriod.Week => $"{GetWeekStart(today):d. MMM} – {GetWeekStart(today).AddDays(6):d. MMM}",
        OverviewPeriod.Month => today.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
        OverviewPeriod.Year => today.Year.ToString(CultureInfo.CurrentCulture),
        _ => string.Empty
    };

    private sealed class Bucket(DateTime startLocal, DateTime endLocal, string label)
    {
        public DateTime StartLocal { get; } = startLocal;
        public DateTime EndLocal { get; } = endLocal;
        public string Label { get; } = label;
        public TimeSpan Duration { get; set; }
    }
}

public sealed record TimeOverview(string Title, string Subtitle, string TotalDurationText, IReadOnlyList<TimeChartBar> Bars);

public sealed record TimeChartBar(string Label, string DurationText, double BarWidth);
