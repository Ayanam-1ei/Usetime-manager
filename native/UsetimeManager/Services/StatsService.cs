using System;
using System.Collections.Generic;
using System.Linq;

namespace UsetimeManager.Services;

public sealed class ChartPoint
{
    public string Label { get; set; } = "";
    public double Ms { get; set; }
}

public sealed class AppUsageItem
{
    public string ProcessName { get; set; } = "";
    public string ExePath { get; set; } = "";
    public double TotalMs { get; set; }
    public double Ratio { get; set; }
}

public sealed class PeriodStats
{
    public double TotalMs { get; set; }
    public double PrevTotalMs { get; set; }
    public double DeltaMs { get; set; }
    public List<ChartPoint> Points { get; set; } = new();
    public List<AppUsageItem> Apps { get; set; } = new();
}

/// <summary>按 日/周/月 聚合会话数据</summary>
public static class StatsService
{
    private static double Overlap(long sStart, long sEnd, long rStart, long rEnd)
    {
        var a = Math.Max(sStart, rStart);
        var b = Math.Min(sEnd, rEnd);
        return b > a ? b - a : 0;
    }

    public static (long start, long end) Range(string period, DateTime date)
    {
        switch (period)
        {
            case "week":
            {
                var dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
                var start = date.Date.AddDays(1 - dow);
                return (ToTs(start), ToTs(start.AddDays(7)) - 1);
            }
            case "month":
            {
                var start = new DateTime(date.Year, date.Month, 1);
                return (ToTs(start), ToTs(start.AddMonths(1)) - 1);
            }
            default:
                return (ToTs(date.Date), ToTs(date.Date.AddDays(1)) - 1);
        }
    }

    public static (long start, long end) PrevRange(string period, DateTime date)
    {
        switch (period)
        {
            case "week":
                return Range("week", date.AddDays(-7));
            case "month":
                return Range("month", new DateTime(date.Year, date.Month, 1).AddMonths(-1));
            default:
                return Range("day", date.AddDays(-1));
        }
    }

    private static long ToTs(DateTime local) =>
        new DateTimeOffset(local).ToUnixTimeMilliseconds();

    public static PeriodStats Compute(IReadOnlyList<UsageSession> sessions, string period, DateTime date)
    {
        var (start, end) = Range(period, date);
        var (pStart, pEnd) = PrevRange(period, date);

        double total = 0, prev = 0;
        foreach (var s in sessions)
        {
            total += Overlap(s.StartTs, s.EndTs, start, end);
            prev += Overlap(s.StartTs, s.EndTs, pStart, pEnd);
        }

        var buckets = BuildBucketRanges(period, date);
        foreach (var s in sessions)
        {
            foreach (var b in buckets)
            {
                b.Ms += Overlap(s.StartTs, s.EndTs, b.Start, b.End);
            }
        }

        var appMap = new Dictionary<string, AppUsageItem>();
        double appTotal = 0;
        foreach (var s in sessions)
        {
            var ms = Overlap(s.StartTs, s.EndTs, start, end);
            if (ms <= 0) continue;
            appTotal += ms;
            var key = string.IsNullOrEmpty(s.ProcessName) ? "unknown" : s.ProcessName;
            if (!appMap.TryGetValue(key, out var item))
            {
                item = new AppUsageItem { ProcessName = key, ExePath = s.ExePath };
                appMap[key] = item;
            }
            item.TotalMs += ms;
            if (string.IsNullOrEmpty(item.ExePath) && !string.IsNullOrEmpty(s.ExePath))
                item.ExePath = s.ExePath;
        }

        foreach (var item in appMap.Values)
        {
            item.Ratio = appTotal > 0 ? item.TotalMs / appTotal : 0;
        }

        return new PeriodStats
        {
            TotalMs = total,
            PrevTotalMs = prev,
            DeltaMs = total - prev,
            Points = buckets.Select(b => new ChartPoint { Label = b.Label, Ms = b.Ms }).ToList(),
            Apps = appMap.Values.OrderByDescending(a => a.TotalMs).ToList()
        };
    }

    private sealed class Bucket
    {
        public string Label = "";
        public long Start;
        public long End;
        public double Ms;
    }

    private static List<Bucket> BuildBucketRanges(string period, DateTime date)
    {
        var list = new List<Bucket>();
        switch (period)
        {
            case "day":
                for (var h = 0; h < 24; h++)
                {
                    var s = date.Date.AddHours(h);
                    list.Add(new Bucket
                    {
                        Label = $"{h:00}:00",
                        Start = ToTs(s),
                        End = ToTs(s.AddHours(1))
                    });
                }
                break;
            case "week":
            {
                var dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
                var start = date.Date.AddDays(1 - dow);
                var names = new[] { "日", "一", "二", "三", "四", "五", "六" };
                for (var i = 0; i < 7; i++)
                {
                    var d = start.AddDays(i);
                    list.Add(new Bucket
                    {
                        Label = $"周{names[(int)d.DayOfWeek]}",
                        Start = ToTs(d),
                        End = ToTs(d.AddDays(1))
                    });
                }
                break;
            }
            default:
            {
                var start = new DateTime(date.Year, date.Month, 1);
                var days = DateTime.DaysInMonth(date.Year, date.Month);
                for (var i = 0; i < days; i++)
                {
                    var d = start.AddDays(i);
                    list.Add(new Bucket
                    {
                        Label = (i + 1).ToString(),
                        Start = ToTs(d),
                        End = ToTs(d.AddDays(1))
                    });
                }
                break;
            }
        }
        return list;
    }
}
