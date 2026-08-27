namespace Measures.Data;

/// <summary>
/// The weeks the app ships with. Every numeral and caption on the page is
/// derived from these records, so changing a value here moves the whole card.
/// The ridge and the sessions carry more history than a card shows at once:
/// the range switchers window onto it, and the prior period is what the
/// captions compare against.
/// </summary>
public static class SampleData
{
    /// <summary>Two weeks of deep work, ending on the most recent Sunday.</summary>
    public static RidgeData RidgeHistory { get; } = BuildRidge(
        [1.9, 3.4, 2.8, 4.1, 2.2, 3.6, 3.9, 2.5, 3.8, 1.2, 4.6, 3.1, 5.2, 4.2]);

    /// <summary>The week the ridge opens on.</summary>
    public static RidgeData Ridge => RidgeHistory.Last(7);

    /// <summary>Four weeks of sessions, ending today.</summary>
    public static SessionLog SessionsHistory { get; } = BuildSessions(
        [2, 4, 3, 1, 3, 5, 2, 4, 2, 3, 0, 5, 3, 2, 3, 5, 2, 0, 4, 6, 3, 1, 4, 5, 2, 6, 3, 4]);

    /// <summary>The fortnight the sessions card opens on.</summary>
    public static SessionLog Sessions => SessionsHistory.Last(14);

    public static SleepNight Night { get; } = new(
        Down: new TimeOnly(23, 4),
        Up: new TimeOnly(6, 36),
        Depths:
        [
            new DepthSample(new TimeOnly(1, 10), 0.806),
            new DepthSample(new TimeOnly(2, 50), 0.419),
            new DepthSample(new TimeOnly(4, 26), 0.742),
        ],
        WindowStart: new TimeOnly(21, 0),
        WindowLength: TimeSpan.FromHours(12));

    public static RunLog Runs { get; } = new(
    [
        new Run("mon", 4.8),
        new Run("wed", 6.1),
        new Run("thu", 5.2),
        new Run("sat", 2.1),
    ],
        GoalKm: 25,
        PaceMinPerKm: 5.8);

    static RidgeData BuildRidge(double[] hours)
    {
        // anchored to a Sunday so the week reads mon..sun whatever today is
        var today = DateOnly.FromDateTime(DateTime.Now);
        var sunday = today.AddDays(-(int)today.DayOfWeek);

        return new RidgeData(
            [.. hours.Select((h, i) => new DayValue(sunday.AddDays(i - hours.Length + 1), h))]);
    }

    static SessionLog BuildSessions(int[] counts)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        return new SessionLog(
            [.. counts.Select((count, i) => new SessionDay(today.AddDays(i - counts.Length + 1), count))]);
    }
}
