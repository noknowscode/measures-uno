namespace Measures.Data;

/// <summary>
/// The week the app ships with. Every numeral and caption on the page is
/// derived from these records, so changing a value here moves the whole card.
/// </summary>
public static class SampleData
{
    public static RidgeData Ridge { get; } = new(
    [
        new DayValue("mon", "monday", 2.5),
        new DayValue("tue", "tuesday", 3.8),
        new DayValue("wed", "wednesday", 1.2),
        new DayValue("thu", "thursday", 4.6),
        new DayValue("fri", "friday", 3.1),
        new DayValue("sat", "saturday", 5.2),
        new DayValue("sun", "sunday", 4.2),
    ]);

    public static SessionLog Sessions { get; } = BuildSessions([3, 5, 2, 0, 4, 6, 3, 1, 4, 5, 2, 6, 3, 4]);

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

    static SessionLog BuildSessions(int[] counts)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        return new SessionLog(
            [.. counts.Select((count, i) => new SessionDay(today.AddDays(i - counts.Length + 1), count))]);
    }
}
