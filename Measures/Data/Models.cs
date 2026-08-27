namespace Measures.Data;

/// <summary>One day on the ridge.</summary>
public readonly record struct DayValue(DateOnly Date, double Hours);

public sealed record RidgeData(IReadOnlyList<DayValue> Days)
{
    public double Total => Days.Sum(d => d.Hours);

    public double Average => Days.Count == 0 ? 0 : Total / Days.Count;

    public double Max => Days.Count == 0 ? 0 : Days.Max(d => d.Hours);

    public DayValue Peak => Days[PeakIndex];

    /// <summary>The meaningful sample: the first day at the window's maximum.</summary>
    public int PeakIndex => Days.Count == 0 ? -1 : Days.IndexOf(Days.MaxBy(d => d.Hours));

    public bool IsEmpty => Days.Count < 2;

    /// <summary>The last <paramref name="count"/> days - a window onto the history.</summary>
    public RidgeData Last(int count) => new(Days.TakeLast(count).ToList());
}

/// <summary>One day of the fortnight of sessions.</summary>
public readonly record struct SessionDay(DateOnly Date, int Count);

public sealed record SessionLog(IReadOnlyList<SessionDay> Days)
{
    public int Total => Days.Sum(d => d.Count);

    public double PerDay => Days.Count == 0 ? 0 : (double)Total / Days.Count;

    public int Max => Days.Count == 0 ? 0 : Days.Max(d => d.Count);

    /// <summary>The meaningful sample: the first day at the window's maximum.</summary>
    public int PeakIndex => Days.Count == 0 ? -1 : Days.IndexOf(Days.MaxBy(d => d.Count));

    public bool IsEmpty => Days.Count == 0;

    public SessionLog Last(int count) => new(Days.TakeLast(count).ToList());

    /// <summary>
    /// The <paramref name="count"/> days before the last <paramref name="count"/>:
    /// the prior week to this week, the prior fortnight to this fortnight.
    /// </summary>
    public SessionLog Before(int count) =>
        new(Days.SkipLast(count).TakeLast(count).ToList());
}

/// <summary>A reading through the night. Depth runs 0 at the horizon to 1 at the deepest.</summary>
public readonly record struct DepthSample(TimeOnly At, double Depth);

public sealed record SleepNight(
    TimeOnly Down,
    TimeOnly Up,
    IReadOnlyList<DepthSample> Depths,
    TimeOnly WindowStart,
    TimeSpan WindowLength)
{
    public TimeSpan Slept => Down <= Up ? Up - Down : TimeSpan.FromDays(1) - (Down - Up);

    public bool IsEmpty => Depths.Count == 0 || WindowLength <= TimeSpan.Zero;

    /// <summary>Where a clock time falls across the window, 0..1.</summary>
    public double Fraction(TimeOnly at)
    {
        var minutes = (at.ToTimeSpan() - WindowStart.ToTimeSpan()).TotalMinutes;

        if (minutes < 0)
        {
            minutes += 24 * 60;
        }

        return minutes / WindowLength.TotalMinutes;
    }

    /// <summary>The clock time at a fraction across the window.</summary>
    public TimeOnly At(double fraction)
    {
        var minutes = WindowStart.ToTimeSpan().TotalMinutes + fraction * WindowLength.TotalMinutes;
        var wrapped = ((minutes % 1440) + 1440) % 1440;

        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(wrapped)));
    }
}

/// <summary>One run on the road.</summary>
public readonly record struct Run(string Day, double Km);

public sealed record RunLog(IReadOnlyList<Run> Runs, double GoalKm, double PaceMinPerKm)
{
    public double Ran => Runs.Sum(r => r.Km);

    public double Left => Math.Max(GoalKm - Ran, 0);

    public double Progress => GoalKm <= 0 ? 0 : Math.Clamp(Ran / GoalKm, 0, 1);

    public bool IsEmpty => Runs.Count == 0 || GoalKm <= 0;
}

static class ListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item) where T : struct
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Equals(item))
            {
                return i;
            }
        }

        return -1;
    }
}
