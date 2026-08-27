using System.Diagnostics;
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
/// A week of running as a road, lit blush - late sun. The gesture is
/// <em>replay</em>: tap a footfall and a runner covers that leg again, leaving a
/// wake of light on the road behind it. The wake is wide and soft and runs
/// under the charcoal progress, so it reads as light rather than as a second
/// line. The runner is the single boldest thing on the page; everything else
/// here stays hairline so it can be.
///
/// The goal lives in the chart: a hollow ring at the road's end, labelled with
/// the target, so the numeral above can be the distance alone.
/// </summary>
public sealed partial class RoadChart : ChartBase
{
    /// <summary>The road, authored in the 300 design space from x 10 to 290.</summary>
    static readonly (double X, double Y)[][] RoadSegments =
    [
        [(DataLeft, 62), (50, 22), (95, 88), (145, 50)],
        [(145, 50), (185, 22), (215, 80), (DataRight, 42)],
    ];

    const double MarchDistance = -18;
    const double GhostDash = 2;
    const double GhostGap = 4;

    /// <summary>The wake holds, then cools. A cancelled run cools four times faster.</summary>
    const double WakeFadeMs = 1200;
    const double WakeCancelMs = 400;

    const double RestRadius = 3;
    const double ArrivedRadius = 5;
    const double ChosenRadius = 3.5;
    const double HoverRadius = 4;

    /// <summary>Design units of breathing room above and below the road inside its box.</summary>
    const double RoadInset = 10;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(RunLog), typeof(RoadChart), new PropertyMetadata(null, OnDataChanged));

    readonly List<DotButton> _marks = [];
    readonly List<(double From, double To)> _bounds = [];
    readonly Stopwatch _clock = new();

    /// <summary>The runner's pace, evaluated per frame rather than key-framed.</summary>
    readonly Func<double, double> _pace = Anim.Curve(Anim.EaseHands);

    Path? _ghost;
    Path? _trace;
    Path? _progress;
    Ellipse? _start;
    Ellipse? _echo;
    Ellipse? _runner;
    DotButton? _goal;
    HaloText? _goalLabel;
    ScaleTransform? _echoScale;

    CubicSampler? _sampler;
    EventHandler<object>? _rendering;
    Ticker? _march;
    int? _selected;
    int? _hovered;
    bool _arrived;
    int _runToken;

    public RoadChart()
    {
        DefaultStyleKey = typeof(RoadChart);

        Unloaded += (_, _) =>
        {
            StopRunner();
            _march?.Dispose();
        };
    }

    public RunLog? ChartData
    {
        get => (RunLog?)GetValue(ChartDataProperty);
        set => SetValue(ChartDataProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        _ghost = GetTemplateChild("PART_Ghost") as Path;
        _trace = GetTemplateChild("PART_Trace") as Path;
        _progress = GetTemplateChild("PART_Progress") as Path;
        _start = GetTemplateChild("PART_Start") as Ellipse;
        _echo = GetTemplateChild("PART_Echo") as Ellipse;
        _runner = GetTemplateChild("PART_Runner") as Ellipse;

        if (_ghost is not null)
        {
            _ghost.PointerPressed += OnRoadAheadPressed;
        }

        if (_echo is not null)
        {
            _echoScale = new ScaleTransform();
            _echo.RenderTransform = _echoScale;
            _echo.RenderTransformOrigin = new Point(0.5, 0.5);
            _echo.Opacity = 0;
        }

        base.OnApplyTemplate();
    }

    static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (RoadChart)d;

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

        ClearMarks();
        HideLabel();

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            _sampler = null;
            return;
        }

        var s = Px;

        var raw = RoadSegments
            .Select(seg => new CubicSegment(
                new Point(seg[0].X * s, seg[0].Y),
                new Point(seg[1].X * s, seg[1].Y),
                new Point(seg[2].X * s, seg[2].Y),
                new Point(seg[3].X * s, seg[3].Y)))
            .ToList();

        // a cubic stays well inside its control polygon, so the authored y range
        // overstates how much of the box the road actually uses. Measure the
        // curve, then remap it to fill the plot - cubics are affine invariant,
        // so moving the control points is exact rather than an approximation.
        var probe = CubicSampler.FromSegments(raw);
        var lowest = probe.Points.Min(p => p.Y);
        var highest = probe.Points.Max(p => p.Y);
        var span = highest - lowest;

        var inset = RoadInset * s;
        var gain = span < 0.001 ? 1 : (height - 2 * inset) / span;
        var offset = inset - gain * lowest;

        Point Fit(Point p) => new(p.X, gain * p.Y + offset);

        var segments = raw
            .Select(seg => new CubicSegment(Fit(seg.P0), Fit(seg.C1), Fit(seg.C2), Fit(seg.P3)))
            .ToList();

        _sampler = CubicSampler.FromSegments(segments);

        // the road ahead is drawn as separate dashes: a stroke dash pattern is
        // not rendered on this target, so the gaps have to be real gaps
        if (_ghost is not null)
        {
            _ghost.Data = _sampler.DashedGeometry(GhostDash, GhostGap);
        }

        if (_trace is not null)
        {
            _trace.Data = null;
            _trace.Opacity = 0;
        }

        // and the road already run is a truncated road, not a partial stroke
        if (_progress is not null)
        {
            _progress.Data = _sampler.GeometryBetween(0, _sampler.TotalLength * data.Progress);
        }

        if (_start is not null)
        {
            Centre(_start, _sampler.Start);
        }

        BuildGoal(data);
        BuildMarks(data);
        Render(animate: false);
    }

    protected override void PlayEntrance()
    {
        var data = ChartData;

        if (data is null || _sampler is null)
        {
            return;
        }

        if (_progress is not null && PlotWidth > 0)
        {
            DrawIn(_progress, PlotWidth, PlotHeight, 1600, 300, Anim.EaseHands);
        }

        for (var i = 0; i < _marks.Count; i++)
        {
            // each footfall settles as the drawn road reaches it
            var reached = data.Ran <= 0 ? 0 : _bounds[i].To / _sampler.TotalLength;
            _marks[i].PlayEntrance(300 + 1600 * reached * 0.9);
        }

        EchoTheGoal(2200);
    }

    protected override void PublishDefault()
    {
        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            Publish(Reading.Default("no runs logged"));
            return;
        }

        Publish(Reading.Default(Invited
            ? string.Create(CultureInfo.InvariantCulture, $"{data.Left:0.#} km remaining")
            : "tap a footfall to replay its run"));
    }

    void BuildGoal(RunLog data)
    {
        if (Plot is null || _sampler is null)
        {
            return;
        }

        _goal = new DotButton { HitSize = 28, BaseSize = 10, FocusSize = 18 };
        AutomationProperties.SetName(_goal, $"Goal, {data.GoalKm:0.#} kilometres. Read the time left to finish.");

        _goal.Click += OnRoadAhead;
        Plot.Children.Add(_goal);
        _goal.MoveTo(_sampler.End.X, _sampler.End.Y);
        _goal.SetColours(Colors.Transparent, Palette.Get(Palette.Token.Ink, 0.5), 1.5, animate: false);
        _goal.SetRadius(4, animate: false);

        // the goal says its number here, so the numeral does not have to
        _goalLabel ??= AddReferenceLabel();
        _goalLabel.Text = string.Create(CultureInfo.InvariantCulture, $"goal {data.GoalKm:0.#}");
        PlaceLabelAbove(_goalLabel, _sampler.End.X - 8, _sampler.End.Y - 6, alignRight: true);

        if (_echo is not null)
        {
            Centre(_echo, _sampler.End);
        }

        if (_runner is not null)
        {
            Centre(_runner, _sampler.Start);
            _runner.Opacity = 0;
        }
    }

    void BuildMarks(RunLog data)
    {
        if (Plot is null || _sampler is null)
        {
            return;
        }

        var covered = 0d;

        for (var i = 0; i < data.Runs.Count; i++)
        {
            var from = covered / data.GoalKm * _sampler.TotalLength;
            covered += data.Runs[i].Km;
            var to = covered / data.GoalKm * _sampler.TotalLength;

            _bounds.Add((from, to));

            var mark = new DotButton { HitSize = 26, BaseSize = 10, FocusSize = 18, Tag = i };
            AutomationProperties.SetName(mark, $"{data.Runs[i].Day}, {data.Runs[i].Km:0.#} kilometres");

            mark.Click += OnMarkClick;
            mark.PointerEntered += OnMarkEntered;
            mark.PointerExited += OnMarkExited;
            mark.LostFocus += OnMarkLostFocus;

            Plot.Children.Add(mark);

            var point = _sampler.PointAtLength(to);
            mark.MoveTo(point.X, point.Y);
            _marks.Add(mark);
        }
    }

    void ClearMarks()
    {
        StopRunner();

        foreach (var mark in _marks)
        {
            mark.Click -= OnMarkClick;
            mark.PointerEntered -= OnMarkEntered;
            mark.PointerExited -= OnMarkExited;
            mark.LostFocus -= OnMarkLostFocus;
            mark.SetBreathing(false);
            Plot?.Children.Remove(mark);
        }

        _marks.Clear();
        _bounds.Clear();

        if (_goal is not null)
        {
            _goal.Click -= OnRoadAhead;
            Plot?.Children.Remove(_goal);
            _goal = null;
        }
    }

    void OnMarkClick(object sender, RoutedEventArgs e)
    {
        var index = (int)((DotButton)sender).Tag;

        MarkInvited();

        if (_selected == index)
        {
            _selected = null;
            StopRunner();
            Render(animate: true);
            return;
        }

        _selected = index;
        _arrived = false;
        Render(animate: true);
        Traverse(index);
    }

    /// <summary>Hover is a preview in the body as well as the caption: the footfall grows and reads.</summary>
    void OnMarkEntered(object sender, PointerRoutedEventArgs e)
    {
        var mark = (DotButton)sender;
        var index = (int)mark.Tag;

        _hovered = index;

        if (!mark.IsBreathing)
        {
            mark.SetRadius(Math.Max(RadiusFor(index), HoverRadius));
        }

        ShowLabel(mark.Centre, ChartData!.Runs[index].Day, ValueFor(index));
        Publish(Reading.Preview(ReadingFor(index)));
    }

    void OnMarkExited(object sender, PointerRoutedEventArgs e) => EndPreview((DotButton)sender);

    void OnMarkLostFocus(object sender, RoutedEventArgs e) => EndPreview((DotButton)sender);

    void EndPreview(DotButton mark)
    {
        if (_hovered != (int)mark.Tag)
        {
            return;
        }

        _hovered = null;

        if (!mark.IsBreathing)
        {
            mark.SetRadius(RadiusFor((int)mark.Tag));
        }

        RenderLabel();
        PublishCurrent();
    }

    double RadiusFor(int index) =>
        _selected != index ? RestRadius
        : _arrived ? ArrivedRadius
        : ChosenRadius;

    void OnRoadAheadPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        RoadAhead();
    }

    void OnRoadAhead(object sender, RoutedEventArgs e) => RoadAhead();

    /// <summary>
    /// The road not yet run, answered in minutes. It reverts on the next
    /// interaction rather than on a timer - nothing here expires on its own.
    /// </summary>
    void RoadAhead()
    {
        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            return;
        }

        MarkInvited();
        StopRunner();
        _selected = null;
        HideLabel();

        if (_ghost is not null && _sampler is { } road)
        {
            var ghost = _ghost;

            _march?.Dispose();
            _march = Ticker.Run(1200, 0, Anim.Curve(Anim.EaseHands),
                eased => ghost.Data = road.DashedGeometry(GhostDash, GhostGap, MarchDistance * eased),
                done: () => ghost.Data = road.DashedGeometry(GhostDash, GhostGap));
        }

        var minutes = (int)Math.Round(data.Left * data.PaceMinPerKm);

        Publish(Reading.Committed(string.Create(
            CultureInfo.InvariantCulture,
            $"{data.Left:0.#} km — about {minutes} min at your pace")));

        RenderMarks(animate: true);
    }

    string ValueFor(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{ChartData!.Runs[index].Km:0.#} km");

    /// <summary>The foot interprets: where this run left the week, not what it was.</summary>
    string ReadingFor(int index)
    {
        var data = ChartData!;
        var after = data.Runs.Take(index + 1).Sum(r => r.Km);

        return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(data.GoalKm - after, 0):0.#} km remaining after this run");
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
        RenderMarks(animate);
        RenderLabel();
        PublishCurrent();
    }

    void RenderLabel()
    {
        if (_selected is { } index && index < _marks.Count)
        {
            ShowLabel(_marks[index].Centre, ChartData!.Runs[index].Day, ValueFor(index));
        }
        else
        {
            HideLabel();
        }
    }

    void RenderMarks(bool animate)
    {
        var last = _marks.Count - 1;

        for (var i = 0; i < _marks.Count; i++)
        {
            // the doctrine: a small hollow mark for a past run, solid for the
            // one being read and for the current one. Chosen then differs from
            // resting on fill AND size, not on size alone
            var isHot = _selected == i || i == last;

            _marks[i].SetColours(
                isHot ? Palette.Get(Palette.Token.Ink) : Palette.Get(Palette.Token.Card),
                Palette.Get(Palette.Token.Ink),
                1.5,
                animate);

            var breathes = i == last && _selected is null;

            _marks[i].SetBreathing(breathes, 3, 4.5);

            if (!breathes)
            {
                // the mark swells only once the runner has merged into it
                _marks[i].SetRadius(RadiusFor(i), animate);
            }
        }
    }

    /// <summary>
    /// Replays one leg. Driven from CompositionTarget.Rendering and unsubscribed
    /// the moment it lands: a held rendering hook pins the compositor at display
    /// refresh for the life of the process.
    /// </summary>
    void Traverse(int index)
    {
        var data = ChartData;

        if (data is null || _sampler is null || _runner is null)
        {
            return;
        }

        var token = ++_runToken;
        var (from, to) = _bounds[index];

        if (!Anim.Enabled)
        {
            Arrive(index, token);
            return;
        }

        var runner = _runner;
        var sampler = _sampler;
        var duration = 400 + data.Runs[index].Km / data.GoalKm * 2500;

        if (_trace is not null)
        {
            _trace.Opacity = 1;
            _trace.Data = null;
        }

        _runner.Opacity = 1;
        _clock.Restart();

        _rendering = (_, _) =>
        {
            if (token != _runToken)
            {
                return;
            }

            var u = Math.Min(_clock.Elapsed.TotalMilliseconds / duration, 1);
            var length = from + (to - from) * _pace(u);

            Centre(runner, sampler.PointAtLength(length));

            if (_trace is not null)
            {
                _trace.Data = sampler.GeometryBetween(from, length);
            }

            if (u < 1)
            {
                return;
            }

            DetachRendering();
            Arrive(index, token);
        };

        CompositionTarget.Rendering += _rendering;
    }

    void Arrive(int index, int token)
    {
        if (token != _runToken)
        {
            return;
        }

        _clock.Stop();

        if (_runner is not null)
        {
            _runner.Opacity = 0;
        }

        // The wake holds, then cools. Gravity, not hands: hands is an ease-OUT,
        // which spends a fade in its first tenth and reads as the light being
        // switched off. Light draining off a road is a fall, and falls ease in.
        if (_trace is not null && Anim.Enabled)
        {
            Anim.Animate(_trace, "Opacity", 1, 0, WakeFadeMs, Anim.EaseGravity);
        }

        _arrived = true;

        if (_selected == index)
        {
            RenderMarks(animate: true);
        }
    }

    void StopRunner()
    {
        _runToken++;
        _arrived = false;
        _clock.Stop();
        DetachRendering();

        if (_runner is not null)
        {
            _runner.Opacity = 0;
        }

        // a cancelled run does not snap the light off the road, it cools it
        if (_trace is { Opacity: > 0 } trace)
        {
            if (Anim.Enabled)
            {
                Anim.Animate(trace, "Opacity", null, 0, WakeCancelMs, Anim.EaseGravity);
            }
            else
            {
                trace.Opacity = 0;
            }
        }
    }

    void DetachRendering()
    {
        if (_rendering is null)
        {
            return;
        }

        CompositionTarget.Rendering -= _rendering;
        _rendering = null;
    }

    void EchoTheGoal(double delayMs)
    {
        if (_echo is null || _echoScale is null || !Anim.Enabled)
        {
            return;
        }

        // one hello from the finish line, and then it never speaks again
        Anim.Animate(_echoScale, "ScaleX", 1, 13 / 4d, 1400, Anim.EaseHands, delayMs);
        Anim.Animate(_echoScale, "ScaleY", 1, 13 / 4d, 1400, Anim.EaseHands, delayMs);
        Anim.Animate(_echo, "Opacity", 0.7, 0, 1400, Anim.EaseHands, delayMs);
    }
}
