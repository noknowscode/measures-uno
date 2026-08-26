using System.Globalization;
using Measures.Data;

namespace Measures.Pages;

/// <summary>
/// Last night, from lights out to waking, scrubbable hour by hour.
/// </summary>
public sealed partial class NightPage : Page
{
    public NightPage()
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
        var night = SampleData.Night;

        Chart.ChartData = night;
        Card.NumeralValue = night.Slept.Hours.ToString(CultureInfo.InvariantCulture);
        Card.NumeralSuffix = " h ";
        Card.NumeralTail = night.Slept.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }
}
