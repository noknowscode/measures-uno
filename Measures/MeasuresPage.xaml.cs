using System.Globalization;
using Measures.Controls;
using Measures.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Measures;

/// <summary>
/// One page, four instruments on a bench. Every numeral is derived from the
/// same record the chart draws, so a card can never say one thing and show
/// another. The ridge and the sessions window onto a longer history; their
/// period labels switch the window, and everything on the card recomputes.
/// </summary>
public sealed partial class MeasuresPage : Page
{
    /// <summary>The bench's breakpoint, the same number the AdaptiveTrigger uses.</summary>
    const double WideWidth = 620;

    readonly List<Ellipse> _pips = [];

    int _ridgeDays = 7;
    int _sessionDays = 14;
    bool? _onBench;

    public MeasuresPage()
    {
        InitializeComponent();

        Bind();

        RidgeCard.RangeToggled += (_, _) =>
        {
            _ridgeDays = _ridgeDays == 7 ? 14 : 7;
            ShowRidge(_ridgeDays);
            RidgeCard.Replay();
        };

        SessionsCard.RangeToggled += (_, _) =>
        {
            _sessionDays = _sessionDays == 7 ? 14 : 7;
            ShowSessions(_sessionDays);
            SessionsCard.Replay();
        };

        Root.SizeChanged += (_, e) => Seat(e.NewSize.Width >= WideWidth);

        Loaded += (_, _) =>
        {
            PadForChrome();
            Seat(Root.ActualWidth >= WideWidth);
            ThemeLabel.Text = ActualTheme == ElementTheme.Dark ? "Light" : "Dark";

            // on activation focus lands on the first focusable control, which
            // scrolls the page down and paints a focus ring nobody asked for.
            // Park it on the root instead; Tab still walks into the bench.
            Root.Focus(FocusState.Programmatic);
            Scroll.ChangeView(null, 0, null, disableAnimation: true);
        };
    }

    /// <summary>
    /// On a phone the field runs under the translucent status bar, which is
    /// the point - the chrome joins the field - but the header must not. The
    /// inset is the gap between the window and its visible bounds, applied
    /// as a margin so the responsive padding setters still own Padding.
    /// </summary>
    void PadForChrome()
    {
        try
        {
            var window = Microsoft.UI.Xaml.Window.Current?.Bounds ?? default;
            var visible = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds;
            var top = Math.Max(visible.Top - window.Top, 0);
            var bottom = Math.Max(window.Bottom - visible.Bottom, 0);

            Wrap.Margin = new Thickness(0, top, 0, bottom);
        }
        catch (Exception)
        {
            // desktop has no system inset to speak of, and nothing to apologise for
        }
    }

    MeasureCard[] Cards => [RidgeCard, SessionsCard, NightCard, RoadCard];

    /// <summary>
    /// Seats the four cards on the bench or in the carousel. One set of cards,
    /// two rooms: re-parenting keeps every pin, range and entrance state the
    /// reader has built up, where a second set of cards would lose it.
    /// </summary>
    void Seat(bool onBench)
    {
        if (_onBench == onBench)
        {
            return;
        }

        _onBench = onBench;

        foreach (var card in Cards)
        {
            Bench.Children.Remove(card);
            Carousel.Items.Remove(card);
        }

        // in the carousel the whole card swipes, so the night plot must claim
        // the finger on press - there is no vertical scroll left to protect
        Night.ScrubOnPress = !onBench;

        if (onBench)
        {
            foreach (var card in Cards)
            {
                Bench.Children.Add(card);
            }

            return;
        }

        foreach (var card in Cards)
        {
            Carousel.Items.Add(card);
        }

        BuildPips();
    }

    void BuildPips()
    {
        Pips.Children.Clear();
        _pips.Clear();

        for (var i = 0; i < Cards.Length; i++)
        {
            var pip = new Ellipse { Width = 5, Height = 5 };
            Pips.Children.Add(pip);
            _pips.Add(pip);
        }

        PaintPips();
    }

    void PaintPips()
    {
        var res = Application.Current.Resources;

        for (var i = 0; i < _pips.Count; i++)
        {
            _pips[i].Fill = (Brush)res[i == Carousel.SelectedIndex ? "InkBrush" : "InkSoft35Brush"];
        }
    }

    void OnCarouselChanged(object sender, SelectionChangedEventArgs e) => PaintPips();

    /// <summary>
    /// Flips the scheme on the window's root frame, which is what Palette
    /// listens to - the page's own Parent is a presenter inside it on some
    /// targets. The label names the scheme a tap will bring, not the one showing.
    /// </summary>
    void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        if (Microsoft.UI.Xaml.Window.Current?.Content is not FrameworkElement host)
        {
            return;
        }

        var toDark = host.ActualTheme != ElementTheme.Dark;

        host.RequestedTheme = toDark ? ElementTheme.Dark : ElementTheme.Light;
        ThemeLabel.Text = toDark ? "Light" : "Dark";
    }

    /// <summary>
    /// Plays the page cold again: every card rises, every instrument draws
    /// itself in. The entrance is the loading state, so this is also the only
    /// way to see it without relaunching.
    /// </summary>
    void OnReplay(object sender, RoutedEventArgs e)
    {
        foreach (var card in Cards)
        {
            card.Replay();
        }
    }

    void Bind()
    {
        ShowRidge(_ridgeDays);
        ShowSessions(_sessionDays);

        var night = SampleData.Night;
        Night.ChartData = night;
        NightCard.NumeralValue = night.Slept.Hours.ToString(CultureInfo.InvariantCulture);
        NightCard.NumeralSuffix = " h ";
        NightCard.NumeralTail = night.Slept.Minutes.ToString("00", CultureInfo.InvariantCulture);

        var runs = SampleData.Runs;
        Road.ChartData = runs;
        // value and unit only: the goal lives in the chart, labelled at the ring
        RoadCard.NumeralValue = runs.Ran.ToString("0.#", CultureInfo.InvariantCulture);
        RoadCard.NumeralSuffix = " km";
        RoadCard.FootRight = runs.Runs.Count == 1 ? "1 run" : $"{runs.Runs.Count} runs";
    }

    /// <summary>The ridge over the last N days: totals, average, peak and axis dates all follow.</summary>
    void ShowRidge(int days)
    {
        var ridge = SampleData.RidgeHistory.Last(days);

        Ridge.ChartData = ridge;
        RidgeCard.NumeralValue = ridge.Total.ToString("0.#", CultureInfo.InvariantCulture);
        // value and unit only: the head already owns the period
        RidgeCard.NumeralSuffix = " h";
        RidgeCard.HeadRight = $"{days} days";
        RidgeCard.AxisLabels = $"{Format.DayLabel(ridge.Days[0].Date)}, {Format.DayLabel(ridge.Days[^1].Date)}";
    }

    void ShowSessions(int days)
    {
        var sessions = SampleData.SessionsHistory.Last(days);

        Sessions.PriorData = SampleData.SessionsHistory.Before(days);
        Sessions.ChartData = sessions;
        SessionsCard.NumeralValue = sessions.Total.ToString(CultureInfo.InvariantCulture);
        SessionsCard.NumeralSuffix = sessions.Total == 1 ? " session" : " sessions";
        SessionsCard.HeadRight = $"{days} days";
        SessionsCard.AxisLabels = $"{Format.DayLabel(sessions.Days[0].Date)}, today";
    }
}
