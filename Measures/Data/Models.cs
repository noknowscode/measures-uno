namespace Measures.Data;

/// <summary>One day on the ridge.</summary>
public readonly record struct DayValue(string Short, string Full, double Hours);

public sealed record RidgeData(IReadOnlyList<DayValue> Days)
{
    public double Total => Days.Sum(d => d.Hours);

    public double Average => Days.Count == 0 ? 0 : Total / Days.Count;

    public double Max => Days.Count == 0 ? 0 : Days.Max(d => d.Hours);

    public DayValue Peak => Days.OrderByDescending(d => d.Hours).First();

    public bool IsEmpty => Days.Count < 2;
}

/// <summary>One day of the fortnight of rain.</summary>
public readonly record struct SessionDay(DateOnly Date, int Count);

public sealed record SessionLog(IReadOnlyList<SessionDay> Days)
{
    public int Total => Days.Sum(d => d.Count);

    public double PerDay => Days.Count == 0 ? 0 : (double)Total / Days.Count;

    public int Max => Days.Count == 0 ? 0 : Days.Max(d => d.Count);

    public bool IsEmpty => Days.Count == 0;
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
