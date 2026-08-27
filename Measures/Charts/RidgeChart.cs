using System.Globalization;
using Measures.Controls;
using Measures.Data;
using Measures.Motion;
using Measures.Theming;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Measures.Charts;

/// <summary>
/// Deep work as a skyline, lit butter - dawn on the ridge. The gesture is
/// <em>pin</em>: choose a day and a plumb line drops from it, so the reading is
/// weighed against the week rather than read off an axis.
///
/// A chosen day is charcoal, larger, and carries the plumb. Three channels, no
/// hue - so the state survives being read in grayscale.
/// </summary>
public sealed partial class RidgeChart : ChartBase
{
    const double Pad = 10;
    const double Ceiling = 6;

    /// <summary>Resting, today, chosen, and under the pointer.</summary>
    const double RestRadius = 2.5;
    const double TodayRadius = 3.5;
    const double PinnedRadius = 4;
    const double HoverRadius = 3.75;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(RidgeData), typeof(RidgeChart), new PropertyMetadata(null, OnDataChanged));

    readonly List<DotButton> _dots = [];

    Path? _fill;
    Path? _line;
    Line? _pin;
    ScaleTransform? _pinScale;
    Point[] _points = [];
    int? _selected;

    public RidgeChart()
    {
        DefaultStyleKey = typeof(RidgeChart);
        PlotHeight = 100;
    }

    public RidgeData? ChartData
    {
        get => (RidgeData?)GetValue(ChartDataProperty);
        set => SetValue(ChartDataProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        _fill = GetTemplateChild("PART_Fill") as Path;
        _line = GetTemplateChild("PART_Line") as Path;
        _pin = GetTemplateChild("PART_Pin") as Line;

        if (_pin is not null)
        {
            _pinScale = new ScaleTransform { ScaleY = 0 };
            _pin.RenderTransform = _pinScale;
            _pin.Opacity = 0;
        }

        base.OnApplyTemplate();
    }

    static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (RidgeChart)d;

        if (chart.PlotWidth > 0)
        {
            chart.Rebuild(chart.PlotWidth, chart.PlotHeight);
            chart.PublishDefault();
        }
    }

    protected override void Rebuild(double width, double height)
    {
        if (Plot is null)
        {
            return;
        }

        ClearDots();

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            _points = [];
            SetPathData(null, null);
            return;
        }

        var days = data.Days;
        var span = width - 2 * Pad;
        var plot = height - 2 * Pad;

        _points = [.. days.Select((day, i) => new Point(
            Pad + i * span / (days.Count - 1),
            height - Pad - day.Hours / Ceiling * plot))];

        var segments = Curves.Midpoint(_points);

        SetPathData(
            Curves.ToGeometry(segments, closeToY: true, closeY: height),
            Curves.ToGeometry(segments));

        BuildDots(days);
        Render(animate: false);
    }

    protected override void PlayEntrance()
    {
        if (_line is not null && PlotWidth > 0)
        {
            DrawIn(_line, PlotWidth, PlotHeight, 1800, 200, Anim.EaseHands);
        }

        if (_fill is not null)
        {
            FadeLate(_fill);
        }
    }

    protected override void PublishDefault()
    {
        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            Publish(Reading.Default("no hours logged"));
            return;
        }

        Publish(Reading.Default(Invited
            ? $"averaging {data.Average:0.0} h — {data.Peak.Full} peaked"
            : "tap a day to weigh it"));
    }

    void SetPathData(Geometry? fill, Geometry? line)
    {
        if (_fill is not null)
        {
            _fill.Data = fill;
        }

        if (_line is not null)
        {
            _line.Data = line;
        }
    }

    void ClearDots()
    {
        foreach (var dot in _dots)
        {
            dot.Click -= OnDotClick;
            dot.PointerEntered -= OnDotEntered;
            dot.PointerExited -= OnDotExited;
            dot.KeyDown -= OnDotKeyDown;
            dot.SetBreathing(false);
            Plot?.Children.Remove(dot);
        }

        _dots.Clear();
    }

    void BuildDots(IReadOnlyList<DayValue> days)
    {
        if (Plot is null)
        {
            return;
        }

        for (var i = 0; i < days.Count; i++)
        {
            var dot = new DotButton
            {
                HitSize = 28,
                BaseSize = 10,
                FocusSize = 16,
                Tag = i,
            };

            AutomationProperties.SetName(dot, $"{days[i].Full}, {days[i].Hours:0.#} hours");

            dot.Click += OnDotClick;
            dot.PointerEntered += OnDotEntered;
            dot.PointerExited += OnDotExited;
            dot.KeyDown += OnDotKeyDown;

            Plot.Children.Add(dot);
            dot.MoveTo(_points[i].X, _points[i].Y);
            _dots.Add(dot);
        }
    }

    void OnDotClick(object sender, RoutedEventArgs e)
    {
        var index = (int)((DotButton)sender).Tag;

        MarkInvited();
        _selected = _selected == index ? null : index;
        Render(animate: true);
    }

    /// <summary>Hover is a preview in the body as well as the caption: the mark grows.</summary>
    void OnDotEntered(object sender, PointerRoutedEventArgs e)
    {
        var dot = (DotButton)sender;
        var index = (int)dot.Tag;

        if (!dot.IsBreathing)
        {
            dot.SetRadius(Math.Max(RadiusFor(index), HoverRadius));
        }

        Publish(Reading.Preview(ReadingFor(index)));
    }

    void OnDotExited(object sender, PointerRoutedEventArgs e)
    {
        var dot = (DotButton)sender;

        if (!dot.IsBreathing)
        {
            dot.SetRadius(RadiusFor((int)dot.Tag));
        }

        PublishCurrent();
    }

    double RadiusFor(int index) =>
        _selected == index ? PinnedRadius
        : index == _dots.Count - 1 ? TodayRadius
        : RestRadius;

    void OnDotKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var index = (int)((DotButton)sender).Tag;

        var next = e.Key switch
        {
            Windows.System.VirtualKey.Right => index + 1,
            Windows.System.VirtualKey.Left => index - 1,
            _ => -1,
        };

        if (next < 0 || next >= _dots.Count)
        {
            return;
        }

        e.Handled = true;
        _dots[next].Focus(FocusState.Keyboard);
        Publish(Reading.Preview(ReadingFor(next)));
    }

    string ReadingFor(int index)
    {
        var data = ChartData!;
        var day = data.Days[index];
        var delta = day.Hours - data.Average;
        var sign = delta >= 0 ? "+" : "−";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{day.Short} — {day.Hours:0.#} h · {sign}{Math.Abs(delta):0.0} vs average");
    }

    void PublishCurrent()
    {
        if (_selected is { } index)
        {
            Publish(Reading.Committed(ReadingFor(index)));
        }
        else
        {
            PublishDefault();
        }
    }

    void Render(bool animate)
    {
        var today = _dots.Count - 1;

        for (var i = 0; i < _dots.Count; i++)
        {
            var isSelected = _selected == i;
            var isToday = i == today;
            var isHot = isSelected || isToday;

            // charcoal fills the mark that is being read; the rest are outlines
            _dots[i].SetColours(
                isHot ? Palette.Get(Palette.Token.Ink) : Palette.Get(Palette.Token.Card),
                Palette.Get(Palette.Token.Ink),
                1.5,
                animate);

            // today breathes only while nothing is pinned - one living thing at a time
            var breathes = isToday && _selected is null;

            _dots[i].SetBreathing(breathes);

            if (!breathes)
            {
                _dots[i].SetRadius(RadiusFor(i), animate);
            }
        }

        RenderPin(animate);
        PublishCurrent();
    }

    void RenderPin(bool animate)
    {
        if (_pin is null || _pinScale is null)
        {
            return;
        }

        if (_selected is not { } index || index >= _points.Length)
        {
            _pin.Opacity = 0;
            return;
        }

        var point = _points[index];
        var top = point.Y + 6;
        var bottom = PlotHeight - 2;

        _pin.X1 = _pin.X2 = point.X;
        _pin.Y1 = top;
        _pin.Y2 = bottom;
        _pin.Opacity = 1;

        // gravity: the plumb drops from the mark, it does not grow from the floor
        _pin.RenderTransformOrigin = new Point(0, 0);
        _pinScale.CenterY = top;
        _pinScale.ScaleY = animate ? 0 : 1;

        if (animate)
        {
            Anim.Animate(_pinScale, "ScaleY", 0, 1, 300, Anim.EaseGravity);
        }
    }
}
