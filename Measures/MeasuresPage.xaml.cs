using System.Globalization;
using Measures.Controls;
using Measures.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Measures;

/// <summary>
/// One page, four instruments read one at a time. Every numeral is derived from the
/// same record the chart draws, so a card can never say one thing and show
/// another. The ridge and the sessions window onto a longer history; their
/// period labels switch the window, and everything on the card recomputes.
/// </summary>
public sealed partial class MeasuresPage : Page
{
    readonly List<Ellipse> _pips = [];
    readonly List<(SpacedText Name, TextBlock Reading)> _index = [];

    int _ridgeDays = 7;
    int _sessionDays = 14;

    public MeasuresPage()
    {
        InitializeComponent();

        Bind();

        RidgeCard.RangeToggled += (_, _) =>
        {
            _ridgeDays = _ridgeDays == 7 ? 14 : 7;
            ShowRidge(_ridgeDays);
            RidgeCard.Replay();
            PaintIndex();
        };

        SessionsCard.RangeToggled += (_, _) =>
        {
            _sessionDays = _sessionDays == 7 ? 14 : 7;
            ShowSessions(_sessionDays);
            SessionsCard.Replay();
            PaintIndex();
        };

        // the whole card swipes, so the night plot claims the finger on press -
        // there is no vertical scroll to protect
        Night.ScrubOnPress = true;
        NightMirror.ScrubOnPress = true;

        WireTiles();
        BuildPips();
        BuildIndex();

        // on a desktop the carousel is arrowed; the root already holds focus
        Root.KeyDown += OnRootKeyDown;

#if FRAMEWATCH
        WatchFrames();
#endif

        Loaded += (_, _) =>
        {
            PadForChrome();
            HideStockArrows();
            ThemeLabel.Text = ActualTheme == ElementTheme.Dark ? "Light" : "Dark";

            // on activation focus lands on the first focusable control, which
            // paints a focus ring nobody asked for. Park it on the root
            // instead; Tab still walks into the card.
            Root.Focus(FocusState.Programmatic);
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

    /// <summary>The overview's tiles, in the same order as the cards they mirror.</summary>
    MeasureCard[] Tiles => [RidgeTile, SessionsTile, NightTile, RoadTile];

    /// <summary>The carousel's last stop: all four at once.</summary>
    int OverviewIndex => Carousel.Items.Count - 1;

    /// <summary>
    /// A tile is a card in a room with the others, so it does not switch
    /// ranges or read on its own: a tap on it turns the carousel to the
    /// instrument's own card, where those things live.
    /// </summary>
    void WireTiles()
    {
        for (var i = 0; i < Tiles.Length; i++)
        {
            var at = i;
            Tiles[i].Tapped += (_, _) => Carousel.SelectedIndex = at;
        }
    }

    /// <summary>Everything the head, numeral and axis say, said again on the tile.</summary>
    static void Mirror(MeasureCard from, MeasureCard to)
    {
        to.NumeralValue = from.NumeralValue;
        to.NumeralSuffix = from.NumeralSuffix;
        to.NumeralTail = from.NumeralTail;
        to.HeadRight = from.HeadRight;
        to.AxisLabels = from.AxisLabels;
        to.FootRight = from.FootRight;
    }

#if FRAMEWATCH
    /// <summary>
    /// Debug only (or -p:FrameWatch=true): a per-second frame summary. This is
    /// the one permanent Rendering subscription in the app, and it lives
    /// only in measuring builds for exactly the reason it is otherwise forbidden.
    /// </summary>
    void WatchFrames()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var last = 0d;
        var secondStart = 0d;
        var frames = 0;
        var over20 = 0;
        var worst = 0d;

        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += (_, _) =>
        {
            var now = clock.Elapsed.TotalMilliseconds;
            var gap = now - last;
            last = now;
            frames++;

            if (gap > 20 && gap < 2000)
            {
                over20++;
            }

            worst = Math.Max(worst, Math.Min(gap, 2000));

            if (now - secondStart < 1000)
            {
                return;
            }

            Console.WriteLine($"Measures: fps={frames} over20={over20} worst={worst:0}ms");
            secondStart = now;
            frames = 0;
            over20 = 0;
            worst = 0;
        };
    }
#endif

    /// <summary>Left and right arrows turn the carousel; Home and End jump to its ends.</summary>
    void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var last = Carousel.Items.Count - 1;

        var next = e.Key switch
        {
            Windows.System.VirtualKey.Left => Math.Max(Carousel.SelectedIndex - 1, 0),
            Windows.System.VirtualKey.Right => Math.Min(Carousel.SelectedIndex + 1, last),
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => last,
            _ => -1,
        };

        if (next < 0 || next == Carousel.SelectedIndex)
        {
            return;
        }

        Carousel.SelectedIndex = next;
        e.Handled = true;
    }

    /// <summary>
    /// The pips are drawn at 5 epx and hit at 24: a pip is a place to tap on a
    /// desktop that has no finger to swipe with, so each sits in a bare button.
    /// </summary>
    void BuildPips()
    {
        Pips.Children.Clear();
        _pips.Clear();

        for (var i = 0; i < Carousel.Items.Count; i++)
        {
            var at = i;
            var pip = new Ellipse { Width = 5, Height = 5 };
            var hit = new Button
            {
                Style = (Style)Application.Current.Resources["PipButtonStyle"],
                Content = pip,
            };

            AutomationProperties.SetName(hit, i < Cards.Length ? Cards[i].HeadLeft : "All four");
            hit.Click += (_, _) => Carousel.SelectedIndex = at;

            Pips.Children.Add(hit);
            _pips.Add(pip);
        }

        PaintPips();
    }

    /// <summary>
    /// The FlipView's stock arrows are Fluent chrome on a page that has none;
    /// the rail, the pips and the arrow keys turn the carousel instead. Found
    /// by part name after the template lands, because the template is Uno's.
    /// </summary>
    void HideStockArrows()
    {
        // the template lands after the page's Loaded, and the FlipView shows
        // its buttons again on pointer-over; a zero box survives both
        Carousel.LayoutUpdated += OnCarouselLayout;

        void OnCarouselLayout(object? _, object __)
        {
            var hidden = 0;

            foreach (var name in new[] { "PreviousButtonHorizontal", "NextButtonHorizontal", "PreviousButtonVertical", "NextButtonVertical" })
            {
                if (FindDescendant(Carousel, name) is FrameworkElement arrow)
                {
                    arrow.MaxWidth = 0;
                    arrow.MaxHeight = 0;
                    arrow.IsHitTestVisible = false;
                    arrow.Visibility = Visibility.Collapsed;
                    hidden++;
                }
            }

            if (hidden == 4)
            {
                Carousel.LayoutUpdated -= OnCarouselLayout;
            }
        }
    }

    static FrameworkElement? FindDescendant(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is FrameworkElement fe && fe.Name == name)
            {
                return fe;
            }

            if (FindDescendant(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    void PaintPips()
    {
        var res = Application.Current.Resources;

        for (var i = 0; i < _pips.Count; i++)
        {
            _pips[i].Fill = (Brush)res[i == Carousel.SelectedIndex ? "InkBrush" : "InkSoft35Brush"];
        }
    }

    void OnCarouselChanged(object sender, SelectionChangedEventArgs e)
    {
        PaintPips();
        PaintIndex();
    }

    /// <summary>
    /// One row per card: its name in the label voice, its reading in the serif
    /// under it. The reading is the card's own numeral, re-read whenever the
    /// card recomputes, so the rail and the card cannot drift apart.
    /// </summary>
    void BuildIndex()
    {
        var res = Application.Current.Resources;

        Index.Children.Clear();
        _index.Clear();

        for (var i = 0; i < Cards.Length; i++)
        {
            var card = Cards[i];
            var at = i;

            var name = new SpacedText
            {
                Style = (Style)res["LabelStyle"],
                Text = card.HeadLeft,
            };

            var reading = new TextBlock
            {
                FontFamily = (FontFamily)res["SerifFont"],
                FontSize = (double)res["TitleSize"],
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.NoWrap,
            };

            var row = new Button
            {
                Style = (Style)res["IndexButtonStyle"],
                Content = new StackPanel { Children = { name, reading } },
            };

            AutomationProperties.SetName(row, card.HeadLeft);
            row.Click += (_, _) => Carousel.SelectedIndex = at;

            Index.Children.Add(row);
            _index.Add((name, reading));
        }

        // and the room with all four in it
        {
            var name = new SpacedText { Style = (Style)res["LabelStyle"], Text = "Overview" };
            var reading = new TextBlock
            {
                FontFamily = (FontFamily)res["SerifItalicFont"],
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                FontSize = (double)res["TitleSize"],
                Margin = new Thickness(0, 6, 0, 0),
                Text = "all four",
            };
            var row = new Button
            {
                Style = (Style)res["IndexButtonStyle"],
                Content = new StackPanel { Children = { name, reading } },
            };

            AutomationProperties.SetName(row, "All four");
            row.Click += (_, _) => Carousel.SelectedIndex = OverviewIndex;

            Index.Children.Add(row);
            _index.Add((name, reading));
        }

        PaintIndex();
    }

    /// <summary>The instrument in the room is ink; the others wait in 35%.</summary>
    void PaintIndex()
    {
        var res = Application.Current.Resources;

        for (var i = 0; i < _index.Count; i++)
        {
            var (name, reading) = _index[i];
            var current = i == Carousel.SelectedIndex;

            if (i < Cards.Length)
            {
                var card = Cards[i];
                reading.Text = card.NumeralValue + card.NumeralSuffix + card.NumeralTail;
            }

            name.Opacity = current ? 1 : 0.55;
            reading.Foreground = (Brush)res[current ? "InkBrush" : "InkSoft35Brush"];
        }
    }

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
        foreach (var card in Cards.Concat(Tiles))
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
        NightMirror.ChartData = night;
        NightCard.NumeralValue = night.Slept.Hours.ToString(CultureInfo.InvariantCulture);
        NightCard.NumeralSuffix = " h ";
        NightCard.NumeralTail = night.Slept.Minutes.ToString("00", CultureInfo.InvariantCulture);

        var runs = SampleData.Runs;
        Road.ChartData = runs;
        // value and unit only: the goal lives in the chart, labelled at the ring
        RoadCard.NumeralValue = runs.Ran.ToString("0.#", CultureInfo.InvariantCulture);
        RoadCard.NumeralSuffix = " km";
        RoadCard.FootRight = runs.Runs.Count == 1 ? "1 run" : $"{runs.Runs.Count} runs";
        RoadMirror.ChartData = runs;

        Mirror(NightCard, NightTile);
        Mirror(RoadCard, RoadTile);
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

        RidgeMirror.ChartData = ridge;
        Mirror(RidgeCard, RidgeTile);
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

        SessionsMirror.PriorData = SampleData.SessionsHistory.Before(days);
        SessionsMirror.ChartData = sessions;
        Mirror(SessionsCard, SessionsTile);
    }
}
