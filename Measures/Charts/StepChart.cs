using System.Globalization;
using Measures.Controls;
using Measures.Data;
using Measures.Motion;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Measures.Charts;

/// <summary>
/// Sessions per day as a step line, lit sage - air after rain. A flat integer
/// plateau per day at that day's count, a sharp vertical step between days,
/// rest days on the baseline: integer data gets integer geometry, angular
/// where the ridge is smooth, and that is this card's uniqueness inside the
/// line family.
///
/// Same anatomy as the ridge: a charcoal 1.5 line with round joins, the
/// temperature cast beneath it, a dashed rule at the true average, a draw-in
/// entrance, and a fixed 0-6 scale that holds across the 7 and 14 day ranges.
/// The gesture is the ridge's <em>pin</em>: tap a day and a plumb drops with
/// the reading at the point. Hit zones are full-height day slots.
/// </summary>
public sealed partial class StepChart : ChartBase
{
    const double Top = 10;
    const double Bottom = 110;

    /// <summary>Fixed, so the axis means the same thing in both ranges. Live data above it clips.</summary>
    const double SMax = 6;

    const double PeakRadius = 2.5;
    const double TodayRadius = 3.5;
    const double TodayBreath = 5;
    const double PinnedRadius = 4;
    const double HoverRadius = 3.75;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(SessionLog), typeof(StepChart), new PropertyMetadata(null, OnDataChanged));

    /// <summary>The same length of days before the window, for the "vs prior" reading.</summary>
    public static readonly DependencyProperty PriorDataProperty = DependencyProperty.Register(
        nameof(PriorData), typeof(SessionLog), typeof(StepChart), new PropertyMetadata(null, OnDataChanged));

    readonly List<DaySlot> _slots = [];

    Path? _fill;
    Path? _line;
    Path? _avg;
    Line? _pin;
    Ellipse? _peak;
    Ellipse? _today;
    Ellipse? _hover;
    Ellipse? _chosen;
    ScaleTransform? _pinScale;
    ScaleTransform? _todayScale;
    HaloText? _avgLabel;
    IDisposable? _breath;

    Point[] _centres = [];
    int? _selected;
    int? _hovered;

    public StepChart()
    {
        DefaultStyleKey = typeof(StepChart);

        Unloaded += (_, _) => _breath?.Dispose();
    }

    public SessionLog? ChartData
    {
        get => (SessionLog?)GetValue(ChartDataProperty);
        set => SetValue(ChartDataProperty, value);
    }

    public SessionLog? PriorData
    {
        get => (SessionLog?)GetValue(PriorDataProperty);
        set => SetValue(PriorDataProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        _fill = GetTemplateChild("PART_Fill") as Path;
        _line = GetTemplateChild("PART_Line") as Path;
        _avg = GetTemplateChild("PART_Avg") as Path;
        _pin = GetTemplateChild("PART_Pin") as Line;
        _peak = GetTemplateChild("PART_Peak") as Ellipse;
        _today = GetTemplateChild("PART_Today") as Ellipse;
        _hover = GetTemplateChild("PART_Hover") as Ellipse;
        _chosen = GetTemplateChild("PART_Chosen") as Ellipse;

        if (_pin is not null)
        {
            _pinScale = new ScaleTransform { ScaleY = 0 };
            _pin.RenderTransform = _pinScale;
            _pin.Opacity = 0;
        }

        if (_today is not null)
        {
            _todayScale = new ScaleTransform();
            _today.RenderTransform = _todayScale;
            _today.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        base.OnApplyTemplate();
    }

    static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (StepChart)d;

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

        ClearSlots();
        HideLabel();

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            _centres = [];
            SetPathData(null, null);
            PlaceRule(_avg, -10);
            return;
        }

        var s = Px;
        var days = data.Days;
        var slot = DataSpan / days.Count;

        var edges = Enumerable.Range(0, days.Count + 1).Select(i => (DataLeft + i * slot) * s).ToList();
        var levels = days.Select(d => YFor(d.Count) * s).ToList();

        _centres = [.. days.Select((d, i) => new Point((DataLeft + (i + 0.5) * slot) * s, YFor(d.Count) * s))];

        var steps = Curves.Steps(edges, levels);

        SetPathData(
            Curves.ToGeometry(steps, closeToY: true, closeY: Bottom * s),
            Curves.ToGeometry(steps));

        var avgY = YFor(data.PerDay) * s;
        PlaceRule(_avg, avgY);

        _avgLabel ??= AddReferenceLabel();
        _avgLabel.Text = string.Create(CultureInfo.InvariantCulture, $"avg {data.PerDay:0.0}");
        PlaceLabelAbove(_avgLabel, DataLeft * s, avgY);

        // the meaningful sample gets a small hollow mark; today rides the line's end
        if (_peak is not null)
        {
            var peak = data.PeakIndex;
            var showPeak = peak >= 0 && peak != days.Count - 1 && days[peak].Count > 0;

            _peak.Opacity = showPeak ? 1 : 0;

            if (showPeak)
            {
                Centre(_peak, _centres[peak]);
            }
        }

        if (_today is not null)
        {
            Centre(_today, new Point(DataRight * s, levels[^1]));
        }

        BuildSlots(days, edges, height);
        Render(animate: false);
    }

    static double YFor(double count) => Bottom - Math.Clamp(count, 0, SMax) / SMax * (Bottom - Top);

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
            Publish(Reading.Default("no sessions logged"));
            return;
        }

        if (!Invited)
        {
            Publish(Reading.Default("tap a day to read it"));
            return;
        }

        var prior = PriorData;
        var period = data.Days.Count == 7 ? "week" : "fortnight";

        Publish(Reading.Default(prior is null || prior.IsEmpty
            ? $"{data.PerDay:0.0} a day"
            : $"{Format.DeltaCount(data.Total - prior.Total)} vs prior {period}"));
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

    void ClearSlots()
    {
        foreach (var slot in _slots)
        {
            slot.Click -= OnSlotClick;
            slot.PointerEntered -= OnSlotEntered;
            slot.PointerExited -= OnSlotExited;
            slot.KeyDown -= OnSlotKeyDown;
            slot.LostFocus -= OnSlotLostFocus;
            Plot?.Children.Remove(slot);
        }

        _slots.Clear();
    }

    void BuildSlots(IReadOnlyList<SessionDay> days, IReadOnlyList<double> edges, double height)
    {
        if (Plot is null)
        {
            return;
        }

        for (var i = 0; i < days.Count; i++)
        {
            var slot = new DaySlot
            {
                Width = edges[i + 1] - edges[i],
                Height = height,
                Tag = i,
            };

            AutomationProperties.SetName(
                slot,
                $"{Format.DayName(days[i].Date)} {days[i].Date.Day}, {Format.Sessions(days[i].Count)}");

            slot.Click += OnSlotClick;
            slot.PointerEntered += OnSlotEntered;
            slot.PointerExited += OnSlotExited;
            slot.KeyDown += OnSlotKeyDown;
            slot.LostFocus += OnSlotLostFocus;

            Plot.Children.Add(slot);
            Place(slot, edges[i], 0);
            _slots.Add(slot);
        }
    }

    void OnSlotClick(object sender, RoutedEventArgs e)
    {
        var index = (int)((DaySlot)sender).Tag;

        MarkInvited();
        _selected = _selected == index ? null : index;
        Render(animate: true);
    }

    void OnSlotEntered(object sender, PointerRoutedEventArgs e) => Preview((int)((DaySlot)sender).Tag);

    void OnSlotExited(object sender, PointerRoutedEventArgs e) => EndPreview();

    void OnSlotLostFocus(object sender, RoutedEventArgs e) => EndPreview();

    void OnSlotKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var index = (int)((DaySlot)sender).Tag;

        var next = e.Key switch
        {
            Windows.System.VirtualKey.Right => index + 1,
            Windows.System.VirtualKey.Left => index - 1,
            _ => -1,
        };

        if (next < 0 || next >= _slots.Count)
        {
            return;
        }

        e.Handled = true;
        _slots[next].Focus(FocusState.Keyboard);
        Preview(next);
    }

    /// <summary>Hover previews in the body and the caption: a hollow mark and the reading at the point.</summary>
    void Preview(int index)
    {
        _hovered = index;

        RenderHover(animate: true);
        ShowLabel(_centres[index], DateFor(index), ValueFor(index));
        Publish(Reading.Preview(ReadingFor(index)));
    }

    void EndPreview()
    {
        if (_hovered is null)
        {
            return;
        }

        _hovered = null;

        RenderHover(animate: true);
        RenderLabel();
        PublishCurrent();
    }

    string DateFor(int index) => Format.DayLabel(ChartData!.Days[index].Date);

    string ValueFor(int index) => Format.Sessions(ChartData!.Days[index].Count);

    string ReadingFor(int index)
    {
        var data = ChartData!;

        return $"{Format.DeltaPercent(data.Days[index].Count, data.PerDay)} vs daily average";
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
        // today breathes only while nothing is pinned - one living thing at a time
        _breath?.Dispose();
        _breath = null;

        if (_today is not null && _todayScale is not null)
        {
            if (_selected is null)
            {
                _breath = BreathClock.BreatheRadius(_todayScale, 1, TodayBreath / TodayRadius);
            }
            else
            {
                _todayScale.ScaleX = _todayScale.ScaleY = 1;
            }
        }

        RenderChosen(animate);
        RenderHover(animate);
        RenderPin(animate);
        RenderLabel();
        PublishCurrent();
    }

    void RenderChosen(bool animate)
    {
        if (_chosen is null)
        {
            return;
        }

        if (_selected is not { } index || index >= _centres.Length)
        {
            Fade(_chosen, 0, animate);
            return;
        }

        Centre(_chosen, _centres[index]);
        Fade(_chosen, 1, animate);
    }

    void RenderHover(bool animate)
    {
        if (_hover is null)
        {
            return;
        }

        // the pointer materialises a hollow mark on an ordinary day; a day that
        // already has a solid one has nothing to add
        if (_hovered is not { } index || index >= _centres.Length || _selected == index || index == _centres.Length - 1)
        {
            Fade(_hover, 0, animate);
            return;
        }

        Centre(_hover, _centres[index]);
        Fade(_hover, 1, animate);
    }

    static void Fade(UIElement element, double to, bool animate)
    {
        if (animate && Anim.Enabled)
        {
            Anim.Animate(element, "Opacity", null, to, 160, Anim.EaseHands);
        }
        else
        {
            element.Opacity = to;
        }
    }

    void RenderLabel()
    {
        if (_selected is { } index && index < _centres.Length)
        {
            ShowLabel(_centres[index], DateFor(index), ValueFor(index));
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

        if (_selected is not { } index || index >= _centres.Length)
        {
            _pin.Opacity = 0;
            return;
        }

        var point = _centres[index];
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
