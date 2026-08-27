using Measures.Controls;
using Measures.Motion;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Measures.Charts;

/// <summary>
/// What every instrument on the bench shares: one plot box, one entrance that
/// plays once, one annotation layer, and a line to say.
///
/// The plot box is 300:120 for all four charts, and every chart's data spans
/// exactly x 10 to 290 of it, so numeral tops, chart boxes and axis rows line
/// up across the bench. Charts author their geometry in that design space and
/// multiply by <see cref="Px"/>; the box follows the card's width and the
/// strokes stay absolute - a hairline is a hairline at any width, which is why
/// this is arithmetic and not a Viewbox.
/// </summary>
public abstract partial class ChartBase : Control, ICaptionSource
{
    public const double DesignWidth = 300;
    public const double DesignHeight = 120;

    /// <summary>The data span. Axis whispers are inset to the same 3.3%.</summary>
    public const double DataLeft = 10;
    public const double DataRight = 290;
    public const double DataSpan = DataRight - DataLeft;

    /// <summary>Reference rules are dashed in real geometry: 2 on, 3 off.</summary>
    const double RuleDash = 2;
    const double RuleGap = 3;

    public static readonly DependencyProperty PlotHeightProperty = DependencyProperty.Register(
        nameof(PlotHeight), typeof(double), typeof(ChartBase), new PropertyMetadata(DesignHeight));

    double _lastWidth;
    bool _entrancePlayed;
    bool _reloading;

    protected ChartBase()
    {
        SizeChanged += OnSizeChanged;

        // a chart moved between the bench and the carousel is unloaded and
        // loaded again; its Unloaded handlers stop the breaths and runners,
        // so a reload rebuilds from the same data to arm them again
        Unloaded += (_, _) => _reloading = true;
        Loaded += (_, _) =>
        {
            if (_reloading && _lastWidth > 0)
            {
                _reloading = false;
                Rebuild(_lastWidth, PlotHeight);
            }
        };
    }

    public event EventHandler<Reading>? ReadingChanged;

    /// <summary>The plot's height, which follows its width at 300:120.</summary>
    public double PlotHeight
    {
        get => (double)GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    protected Canvas? Plot { get; private set; }

    /// <summary>The two-line reading at a point, when the template has one.</summary>
    protected PointLabel? Label { get; private set; }

    /// <summary>True once the reader has performed this chart's gesture.</summary>
    protected bool Invited { get; private set; }

    protected double PlotWidth => _lastWidth;

    /// <summary>Pixels per design unit. Not "Scale": UIElement already has one.</summary>
    protected double Px => _lastWidth / DesignWidth;

    public void PublishInitialReading() => PublishDefault();

    /// <summary>
    /// Replays the entrance from a clean slate. Rebuilding first matters: the
    /// entrance animates from a known starting state, and a chart part-way
    /// through an interaction is not one.
    /// </summary>
    public void Replay()
    {
        if (_lastWidth <= 0)
        {
            return;
        }

        Rebuild(_lastWidth, PlotHeight);
        PlayEntrance();
        PublishDefault();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Plot = GetTemplateChild("PART_Plot") as Canvas;
        Label = GetTemplateChild("PART_Label") as PointLabel;

        if (ActualWidth > 0)
        {
            Layout(ActualWidth);
        }
    }

    /// <summary>Lays the chart out at a given size. Called on every resize.</summary>
    protected abstract void Rebuild(double width, double height);

    /// <summary>The entrance is the loading state; it plays once, after first layout.</summary>
    protected abstract void PlayEntrance();

    /// <summary>The line the card shows when nothing is selected.</summary>
    protected abstract void PublishDefault();

    protected void Publish(Reading reading) => ReadingChanged?.Invoke(this, reading);

    protected void MarkInvited() => Invited = true;

    protected void ShowLabel(Point at, string date, string value) =>
        Label?.Show(at, date, value, PlotWidth, PlotHeight);

    protected void HideLabel() => Label?.Hide();

    /// <summary>A dashed reference rule across the data span, at a height in pixels.</summary>
    protected void PlaceRule(Path? rule, double y)
    {
        if (rule is not null)
        {
            rule.Data = Curves.DashedRule(DataLeft * Px, DataRight * Px, y, RuleDash, RuleGap);
        }
    }

    /// <summary>
    /// A reference label in the one style every reference shares: the tiny
    /// mono whisper with a card-coloured halo. Its value is always computed by
    /// the caller, never typed.
    /// </summary>
    protected HaloText AddReferenceLabel()
    {
        var res = Application.Current.Resources;
        var label = new HaloText((double)res["ChartLabelSize"], (Brush)res["InkSoft45Brush"]);

        Plot?.Children.Add(label);

        return label;
    }

    /// <summary>
    /// Sits a label with its foot 3 epx above y, starting at x - or ending
    /// there, when it must keep clear of something to its right. A rule's
    /// label lives at the left end of the span: the right end is where the
    /// current mark rides the line, and the two were colliding.
    /// </summary>
    protected void PlaceLabelAbove(HaloText label, double x, double y, bool alignRight = false)
    {
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = label.DesiredSize.Width;
        var height = label.DesiredSize.Height;
        var left = alignRight ? x - width : x;

        Canvas.SetLeft(label, Math.Clamp(left, 0, Math.Max(PlotWidth - width, 0)));
        Canvas.SetTop(label, Math.Max(y - 3 - height, 0));
    }

    /// <summary>
    /// Reveals a stroked path from its left edge. The dash-offset draw-in the
    /// prototype uses is unavailable here - StrokeDashArray does not render on
    /// this target - so the line is wiped in behind a widening clip instead.
    /// Every curve on this page is monotonic in x, so a wipe and a draw-in are
    /// the same gesture.
    /// </summary>
    protected static Ticker DrawIn(FrameworkElement path, double width, double height, double ms, double delayMs, KeySpline spline)
    {
        // generous vertically: the clip must not shave the stroke or its caps
        var clip = new RectangleGeometry { Rect = new Rect(0, -height, 0, height * 3) };
        path.Clip = clip;

        return Ticker.Run(ms, delayMs, Anim.Curve(spline),
            eased => clip.Rect = new Rect(0, -height, width * eased, height * 3),
            done: () => path.Clip = null);
    }

    /// <summary>The late fade the mist and the sky share: hold, then arrive.</summary>
    protected static void FadeLate(UIElement element, double totalMs = 2400)
    {
        element.Opacity = 0;
        Anim.Animate(element, "Opacity", 0, 1, totalMs * 0.6, Anim.EaseHands, totalMs * 0.4);
    }

    protected static void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
    }

    protected static void Centre(FrameworkElement element, Point at) =>
        Place(element, at.X - element.Width / 2, at.Y - element.Height / 2);

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;

        if (width <= 0 || Math.Abs(width - _lastWidth) < 0.5)
        {
            return;
        }

        Layout(width);
    }

    void Layout(double width)
    {
        _lastWidth = width;

        // the box keeps its proportion; the height it takes follows from here
        PlotHeight = width * DesignHeight / DesignWidth;

        Rebuild(width, PlotHeight);

        if (_entrancePlayed)
        {
            return;
        }

        _entrancePlayed = true;
        PlayEntrance();
    }
}
