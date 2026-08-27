using System.Globalization;

namespace Measures.Data;

/// <summary>The page speaks in lower case, so the formatting lives in one place.</summary>
public static class Format
{
    static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-CA");

    /// <summary>A day of the fortnight, as the axis and the point labels say it: "tue 12".</summary>
    public static string DayLabel(DateOnly date) =>
        date.ToString("ddd d", Culture).ToLowerInvariant();

    /// <summary>The weekday alone, for a screen reader: "tuesday".</summary>
    public static string DayName(DateOnly date) =>
        date.ToString("dddd", Culture).ToLowerInvariant();

    /// <summary>A clock reading with no leading zero on the hour: "2:10", "23:04".</summary>
    public static string Clock(TimeOnly time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hour}:{time.Minute:00}");

    /// <summary>A signed whole percentage against a base: "+12 %", "−8 %".</summary>
    public static string DeltaPercent(double value, double against)
    {
        if (against <= 0)
        {
            return "—";
        }

        var pct = (value - against) / against * 100;

        return string.Create(CultureInfo.InvariantCulture, $"{Sign(pct)}{Math.Abs(Math.Round(pct)):0} %");
    }

    /// <summary>A signed count: "+3", "−1", "±0".</summary>
    public static string DeltaCount(int delta) =>
        delta == 0 ? "±0" : string.Create(CultureInfo.InvariantCulture, $"{Sign(delta)}{Math.Abs(delta)}");

    /// <summary>A signed hours reading: "+0.7 h".</summary>
    public static string DeltaHours(double delta) =>
        string.Create(CultureInfo.InvariantCulture, $"{Sign(delta)}{Math.Abs(delta):0.0} h");

    /// <summary>A true minus sign, not a hyphen: the numerals deserve it.</summary>
    public static string Sign(double value) => value < 0 ? "−" : "+";

    public static string Sessions(int count) => count == 1 ? "1 session" : $"{count} sessions";
}
