using System.Globalization;

namespace Measures.Data;

/// <summary>The page speaks in lower case, so the formatting lives in one place.</summary>
public static class Format
{
    static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-CA");

    /// <summary>A day of the fortnight, as the rain axis and captions say it: "tue 12".</summary>
    public static string DayLabel(DateOnly date) =>
        date.ToString("ddd d", Culture).ToLowerInvariant();

    /// <summary>A clock reading with no leading zero on the hour: "2:10", "23:04".</summary>
    public static string Clock(TimeOnly time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hour}:{time.Minute:00}");
}
