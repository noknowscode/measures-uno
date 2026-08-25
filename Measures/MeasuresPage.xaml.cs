using System.Globalization;
using Measures.Data;

namespace Measures;

/// <summary>
/// One page, four instruments on a bench. Every numeral is derived from the
/// same record the chart draws, so a card can never say one thing and show
/// another.
/// </summary>
public sealed partial class MeasuresPage : Page
{
    public MeasuresPage()
    {
        InitializeComponent();

        Bind();

        // on activation focus lands on the first chart mark, and the scroll
        // viewer brings it into view - the page would open part way down
        Loaded += (_, _) => Scroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    void Bind()
    {
        var ridge = SampleData.Ridge;
        Ridge.ChartData = ridge;
        RidgeCard.NumeralValue = ridge.Total.ToString("0.#", CultureInfo.InvariantCulture);
        RidgeCard.NumeralSuffix = " h this week";

        var sessions = SampleData.Sessions;
        Rain.ChartData = sessions;
        RainCard.NumeralValue = sessions.Total.ToString(CultureInfo.InvariantCulture);
        RainCard.NumeralSuffix = sessions.Total == 1 ? " session" : " sessions";
        RainCard.AxisLabels = $"{Format.DayLabel(sessions.Days[0].Date)}, today";

        var night = SampleData.Night;
        NightCard.NumeralValue = night.Slept.Hours.ToString(CultureInfo.InvariantCulture);
        NightCard.NumeralSuffix = " h ";
        NightCard.NumeralTail = night.Slept.Minutes.ToString("00", CultureInfo.InvariantCulture);

        var runs = SampleData.Runs;
        RoadCard.NumeralValue = runs.Ran.ToString("0.#", CultureInfo.InvariantCulture);
        RoadCard.NumeralSuffix = $" of {runs.GoalKm:0.#} km";
        RoadCard.FootRight = runs.Runs.Count == 1 ? "1 run" : $"{runs.Runs.Count} runs";
    }
}
