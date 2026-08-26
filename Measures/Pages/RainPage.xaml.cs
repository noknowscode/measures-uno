using System.Globalization;
using Measures.Data;

namespace Measures.Pages;

/// <summary>
/// Fourteen days of focus sessions, each one a bead falling into its day.
/// </summary>
public sealed partial class RainPage : Page
{
    public RainPage()
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
        var sessions = SampleData.Sessions;

        Chart.ChartData = sessions;
        Card.NumeralValue = sessions.Total.ToString(CultureInfo.InvariantCulture);
        Card.NumeralSuffix = sessions.Total == 1 ? " session" : " sessions";
        Card.AxisLabels = $"{Format.DayLabel(sessions.Days[0].Date)}, today";
    }
}
