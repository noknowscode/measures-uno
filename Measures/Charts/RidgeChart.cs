using System.Globalization;
using Measures.Controls;
using Measures.Data;
using Measures.Motion;
using Measures.Theming;
using Microsoft.UI;
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
/// Marks follow the dot doctrine. An ordinary day has no marker and grows a
/// hollow one under the pointer; the peak carries a small hollow one; today and
/// the chosen day are solid. A chosen day is charcoal, larger, and carries the
/// plumb - three channels, no hue - so the state survives being read in
/// grayscale. A dashed rule at the true average, labelled, is the reference.
/// </summary>
public sealed partial class RidgeChart : ChartBase
{
    /// <summary>Design units: the plot keeps 10 above and below the data.</summary>
    const double Top = 10;
    const double Bottom = 110;
    const double Ceiling = 6;

    /// <summary>Ordinary, peak, today, chosen, and under the pointer.</summary>
    const double RestRadius = 2.5;
    const double TodayRadius = 3.5;
    const double PinnedRadius = 4;
    const double HoverRadius = 3.75;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(RidgeData), typeof(RidgeChart), new PropertyMetadata(null, OnDataChanged));

    readonly List<DotButton> _dots = [];

    Path? _fill;
    Path? _line;
    Path? _avg;
    Line? _pin;
    ScaleTransform? _pinScale;
    HaloText? _avgLabel;
    Point[] _points = [];
    int? _selected;
    int? _hovered;

    public RidgeChart()
    {
        DefaultStyleKey = typeof(RidgeChart);
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
        _avg = GetTemplateChild("PART_Avg") as Path;
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

        chart._selected = null;
        chart._hovered = null;

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
        HideLabel();

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            _points = [];
            SetPathData(null, null);
            PlaceRule(_avg, -10);
            return;
        }

        var s = Px;
        var days = data.Days;

        _points = [.. days.Select((day, i) => new Point(
            (DataLeft + i * DataSpan / (days.Count - 1)) * s,
            YFor(day.Hours) * s))];

        var segments = Curves.Midpoint(_points);

        SetPathData(
            Curves.ToGeometry(segments, closeToY: true, closeY: DesignHeight * s),
            Curves.ToGeometry(segments));

        // the reference: a rule at the true average, and its value said once
        var avgY = YFor(data.Average) * s;
        PlaceRule(_avg, avgY);

        _avgLabel ??= AddReferenceLabel();
        _avgLabel.Text = string.Create(CultureInfo.InvariantCulture, $"avg {data.Average:0.0} h");
        PlaceLabelAbove(_avgLabel, DataLeft * s, avgY);

        BuildDots(days);
        Render(animate: false);
    }

    static double YFor(double hours) => Bottom - Math.Clamp(hours, 0, Ceiling) / Ceiling * (Bottom - Top);

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

        // the foot interprets; it never repeats what the chart already shows
        Publish(Reading.Default(Invited
            ? $"today {Format.DeltaHours(data.Days[^1].Hours - data.Average)} vs the {data.Days.Count}-day average"
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
            dot.LostFocus -= OnDotLostFocus;
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

        // a fortnight packs the days closer; the hit zones share the pitch
        var pitch = days.Count < 2 ? 28 : DataSpan * Px / (days.Count - 1);
        var hit = Math.Clamp(pitch, 16, 28);

        for (var i = 0; i < days.Count; i++)
        {
            var dot = new DotButton
            {
                HitSize = hit,
                BaseSize = 10,
                FocusSize = 16,
                Tag = i,
            };

            AutomationProperties.SetName(dot, string.Create(
                CultureInfo.InvariantCulture,
                $"{Format.DayName(days[i].Date)} {days[i].Date.Day}, {days[i].Hours:0.#} hours"));

            dot.Click += OnDotClick;
            dot.PointerEntered += OnDotEntered;
            dot.PointerExited += OnDotExited;
            dot.KeyDown += OnDotKeyDown;
            dot.LostFocus += OnDotLostFocus;

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

    /// <summary>Hover is a preview in the body as well as the caption: a hollow mark materialises and the reading appears.</summary>
    void OnDotEntered(object sender, PointerRoutedEventArgs e) => Preview((int)((DotButton)sender).Tag);

    void OnDotExited(object sender, PointerRoutedEventArgs e) => EndPreview();

    void OnDotLostFocus(object sender, RoutedEventArgs e) => EndPreview();

    void Preview(int index)
    {
        _hovered = index;

        var dot = _dots[index];

        if (!dot.IsBreathing)
        {
            dot.SetRadius(Math.Max(RadiusFor(index), HoverRadius));
        }

        // an ordinary day has no mark until the pointer asks for one
        Paint(index, animate: true);
        ShowLabel(_points[index], DateFor(index), ValueFor(index));
        Publish(Reading.Preview(ReadingFor(index)));
    }

    void EndPreview()
    {
        if (_hovered is not { } index)
        {
            return;
        }

        _hovered = null;

        var dot = _dots[index];

        if (!dot.IsBreathing)
        {
            dot.SetRadius(RadiusFor(index));
        }

        Paint(index, animate: true);
        RenderLabel();
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
        Preview(next);
    }

    string DateFor(int index) => Format.DayLabel(ChartData!.Days[index].Date);

    string ValueFor(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{ChartData!.Days[index].Hours:0.#} h");

    string ReadingFor(int index)
    {
        var data = ChartData!;

        return $"{Format.DeltaPercent(data.Days[index].Hours, data.Average)} vs the {data.Days.Count}-day average";
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
            Paint(i, animate);

            // today breathes only while nothing is pinned - one living thing at a time
            var breathes = i == today && _selected is null;

            _dots[i].SetBreathing(breathes);

            if (!breathes)
            {
                _dots[i].SetRadius(RadiusFor(i), animate);
            }
        }

        RenderPin(animate);
        RenderLabel();
        PublishCurrent();
    }

    /// <summary>
    /// The dot doctrine, per mark: solid for the chosen day and for today,
    /// hollow for the peak and for whatever the pointer is over, and nothing
    /// at all for an ordinary day.
    /// </summary>
    void Paint(int index, bool animate)
    {
        var isSolid = _selected == index || index == _dots.Count - 1;
        var isHollow = _hovered == index || index == ChartData?.PeakIndex;

        var ink = Palette.Get(Palette.Token.Ink);

        var (fill, stroke) =
            isSolid ? (ink, ink)
            : isHollow ? (Palette.Get(Palette.Token.Card), ink)
            : (Colors.Transparent, Colors.Transparent);

        _dots[index].SetColours(fill, stroke, 1.5, animate);
    }

    void RenderLabel()
    {
        if (_selected is { } index && index < _points.Length)
        {
            ShowLabel(_points[index], DateFor(index), ValueFor(index));
        }
        else
        {
            HideLabel();
        }
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
        var bottom = (DesignHeight - 2) * Px;

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
