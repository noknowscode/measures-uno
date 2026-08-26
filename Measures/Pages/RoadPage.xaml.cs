using System.Globalization;
using Measures.Data;

namespace Measures.Pages;

/// <summary>
/// The week's running as one road: what has been covered, and what is left.
/// </summary>
public sealed partial class RoadPage : Page
{
    public RoadPage()
    {
        InitializeComponent();

        // the field bends around the card, so it has to be told which one
        Field.Source = Card;

        Bind();

        Loaded += (_, _) =>
        {
            // on activation focus lands on the first focusable control, which
            // scrolls the page down and paints a focus ring nobody asked for.
            // Park it on the root instead; Tab still walks into the instrument.
            Root.Focus(FocusState.Programmatic);
            Scroll.ChangeView(null, 0, null, disableAnimation: true);
        };
    }

    /// <summary>
    /// Plays the instrument cold again: it draws itself in from nothing. The
    /// card does not rise a second time - the rise is an arrival, and this one
    /// has already arrived.
    /// </summary>
    void OnReplay(object? sender, EventArgs e) => Card.Replay();

    void Bind()
    {
        var runs = SampleData.Runs;

        Chart.ChartData = runs;
        Card.NumeralValue = runs.Ran.ToString("0.#", CultureInfo.InvariantCulture);
        Card.NumeralSuffix = $" of {runs.GoalKm:0.#} km";
        Card.FootRight = runs.Runs.Count == 1 ? "1 run" : $"{runs.Runs.Count} runs";
    }
}
