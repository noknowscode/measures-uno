using System.Globalization;
using Measures.Data;

namespace Measures.Pages;

/// <summary>
/// Deep work over seven days, as a ridge line with the mist under it.
/// </summary>
public sealed partial class RidgePage : Page
{
    public RidgePage()
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
        var ridge = SampleData.Ridge;

        Chart.ChartData = ridge;
        Card.NumeralValue = ridge.Total.ToString("0.#", CultureInfo.InvariantCulture);
        Card.NumeralSuffix = " h this week";
    }
}
