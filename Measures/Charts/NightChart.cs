using Measures.Controls;
using Measures.Data;
using Measures.Motion;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Measures.Charts;

/// <summary>
/// A night as a valley, lit lavender above and mist-blue below - dusk. The
/// gesture is <em>scrub</em>: press and wander, and the caption reads the clock
/// and the depth under your finger. The curve itself is charcoal like every
/// other line on the page; the night is told by the light around it, not by
/// colouring the data.
///
/// To a screen reader it is a slider, not a picture: minimum, maximum, a live
/// value, and a value text that says what the value means ("2:10, deep sleep").
/// </summary>
public sealed partial class NightChart : ChartBase
{
    const double HorizonY = 14;
    const double PlotBottom = 76;
    const double Depth = PlotBottom - HorizonY;
    const double EdgeInset = 4;

    /// <summary>Horizontal travel, in pixels, before a drag counts as a scrub.</summary>
    const double IntentThreshold = 8;

    /// <summary>Arrow keys move by this fraction of the night per press.</summary>
    const double KeyStep = 6.0 / 300;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(SleepNight), typeof(NightChart), new PropertyMetadata(null, OnDataChanged));

    Rectangle? _sky;
    Line? _horizon;
    Path? _rest;
    Path? _curve;
    Ellipse? _wake;
    Line? _scrubLine;
    Ellipse? _scrubDot;
    ScaleTransform? _wakeScale;
    IDisposable? _breath;

    CubicSampler? _sampler;
    double _downX;
    double _upX;

    Point _pressOrigin;
    bool _awaitingIntent;
    bool _scrubbing;
    double? _keyX;

    // what the slider currently reads, in both the machine's terms and a person's
    double _value;
    string _valueText = string.Empty;

    public NightChart()
    {
        DefaultStyleKey = typeof(NightChart);
        PlotHeight = 80;

        // ManipulationMode is deliberately left alone. Making this a manipulation
        // target hands the pointer to the gesture processor, which takes the
        // capture straight back off us and ends the scrub on the first move.
        // Not capturing until horizontal intent is proven is what keeps a
        // vertical swipe with the ScrollViewer, and it needs no manipulation.

        // Setting ManipulationMode makes this element a manipulation target, and
        // the gesture machinery marks the pointer events handled before they
        // reach an ordinary subscription. handledEventsToo is the only way to
        // see them - a plain += here is silently never called.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(PointerExitedEvent, new PointerEventHandler(OnPointerExited), true);
        KeyDown += OnKeyDown;
        LostFocus += (_, _) => EndScrub();

        // the chart advertises its gesture before anyone tries it
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

        Unloaded += (_, _) => _breath?.Dispose();
    }

    public SleepNight? ChartData
    {
        get => (SleepNight?)GetValue(ChartDataProperty);
        set => SetValue(ChartDataProperty, value);
    }

    /// <summary>Where through the night the reader is, 0 to 1.</summary>
    internal double Value => _value;

    /// <summary>What that position means, in the same words the caption uses.</summary>
    internal string ValueText => _valueText;

    protected override AutomationPeer OnCreateAutomationPeer() => new NightAutomationPeer(this);

    /// <summary>Drives the scrub from assistive tech, in the peer's 0-to-1 terms.</summary>
    internal void ScrubToFraction(double fraction)
    {
        if (PlotWidth <= 0)
        {
            return;
        }

        _keyX = Math.Clamp(fraction, 0, 1) * PlotWidth;
        ScrubTo(_keyX.Value);
    }

    protected override void OnApplyTemplate()
    {
        _sky = GetTemplateChild("PART_Sky") as Rectangle;
        _horizon = GetTemplateChild("PART_Horizon") as Line;
        _rest = GetTemplateChild("PART_Rest") as Path;
        _curve = GetTemplateChild("PART_Curve") as Path;
        _wake = GetTemplateChild("PART_Wake") as Ellipse;
        _scrubLine = GetTemplateChild("PART_ScrubLine") as Line;
        _scrubDot = GetTemplateChild("PART_ScrubDot") as Ellipse;

        if (_wake is not null)
        {
            _wakeScale = new ScaleTransform();
            _wake.RenderTransform = _wakeScale;
            _wake.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        base.OnApplyTemplate();
    }

    static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (NightChart)d;

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

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            _sampler = null;
            return;
        }

        AutomationProperties.SetName(
            this,
            $"Sleep depth through the night. Asleep {Format.Clock(data.Down)}, awake {Format.Clock(data.Up)}. " +
            "Drag across it, or use the arrow keys, to read the time and depth.");

        if (_sky is not null)
        {
            _sky.Width = width;
            _sky.Height = PlotBottom;
        }

        if (_horizon is not null)
        {
            _horizon.X1 = 0;
            _horizon.X2 = width;
            _horizon.Y1 = _horizon.Y2 = HorizonY;
        }

        _downX = data.Fraction(data.Down) * width;
        _upX = data.Fraction(data.Up) * width;

        var nodes = new List<Point> { new(_downX, HorizonY) };
        nodes.AddRange(data.Depths.Select(d => new Point(
            data.Fraction(d.At) * width,
            HorizonY + Math.Clamp(d.Depth, 0, 1) * Depth)));
        nodes.Add(new Point(_upX, HorizonY));

        var segments = Curves.FlatTangent(nodes, tension: 0.42, leadIn: 0.35, leadOut: 0.26);

        // falling asleep dives and waking climbs; both would read as a lazy
        // plateau if their outer control points stayed level with the horizon
        var first = segments[0];
        segments[0] = first with
        {
            C1 = new Point(first.C1.X, first.P0.Y + (first.P3.Y - first.P0.Y) * 0.6),
        };

        var last = segments[^1];
        segments[^1] = last with
        {
            C2 = new Point(last.C2.X, last.P3.Y + (last.P0.Y - last.P3.Y) * 0.435),
        };

        _sampler = CubicSampler.FromSegments(segments);

        if (_curve is not null)
        {
            _curve.Data = Curves.ToGeometry(segments);
        }

        if (_rest is not null)
        {
            _rest.Data = Curves.ToGeometry(segments, closeToY: true, closeY: HorizonY);
        }

        if (_wake is not null)
        {
            Place(_wake, _upX - _wake.Width / 2, HorizonY - _wake.Height / 2);
        }

        if (_scrubLine is not null)
        {
            _scrubLine.Y1 = HorizonY;
            _scrubLine.Y2 = PlotBottom;
        }
    }

    protected override void PlayEntrance()
    {
        if (_curve is not null && PlotWidth > 0)
        {
            DrawIn(_curve, PlotWidth, PlotHeight, 1800, 200, Anim.EaseHands);
        }

        if (_sky is not null)
        {
            FadeLate(_sky);
        }

        if (_rest is not null)
        {
            FadeLate(_rest);
        }

        if (_wake is not null && _wakeScale is not null)
        {
            _breath?.Dispose();
            _breath = BreathClock.BreatheRadius(_wakeScale, 1, 5.0 / 3.5);
        }
    }

    protected override void PublishDefault()
    {
        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            Publish(Reading.Default("no sleep recorded"));
            return;
        }

        Publish(Reading.Default(Invited
            ? $"asleep {Format.Clock(data.Down)} — awake {Format.Clock(data.Up)}"
            : "press and wander the night"));
    }

    static string DepthWord(double depth) =>
        depth >= 0.661 ? "deep" : depth >= 0.258 ? "light" : "drifting";

    void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {

        if (_sampler is null)
        {
            return;
        }

        Focus(FocusState.Pointer);
        _pressOrigin = e.GetCurrentPoint(this).Position;

        // a pointer that cannot wander vertically can start scrubbing at once;
        // a finger has to prove it means to go sideways first
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
        {
            _awaitingIntent = true;
            return;
        }

        BeginScrub(e);
        ScrubTo(_pressOrigin.X);
    }

    void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        if (_awaitingIntent)
        {
            var dx = Math.Abs(position.X - _pressOrigin.X);
            var dy = Math.Abs(position.Y - _pressOrigin.Y);

            if (dy > IntentThreshold && dy >= dx)
            {
                // that was a scroll; let it go
                _awaitingIntent = false;
                return;
            }

            if (dx <= IntentThreshold || dx <= dy)
            {
                return;
            }

            _awaitingIntent = false;
            BeginScrub(e);
        }

        if (_scrubbing)
        {
            ScrubTo(position.X);
        }
    }

    void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _awaitingIntent = false;

        if (!_scrubbing)
        {
            return;
        }

        ReleasePointerCaptures();
        EndScrub();
    }

    void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        // an active scrub owns the pointer; only a release or a lost capture
        // ends it, or dragging past the edge of the card would cut it short
        if (_scrubbing)
        {
            return;
        }

        _awaitingIntent = false;
    }

    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_sampler is null)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            EndScrub();
            return;
        }

        var step = e.Key switch
        {
            Windows.System.VirtualKey.Right => KeyStep,
            Windows.System.VirtualKey.Left => -KeyStep,
            _ => 0,
        };

        if (step == 0)
        {
            return;
        }

        e.Handled = true;

        var width = PlotWidth;
        var from = _keyX ?? width / 2;

        _keyX = Math.Clamp(from + step * width, EdgeInset, width - EdgeInset);
        ScrubTo(_keyX.Value);
    }

    void BeginScrub(PointerRoutedEventArgs e)
    {
        _scrubbing = CapturePointer(e.Pointer);
    }

    void ScrubTo(double x)
    {
        var data = ChartData;

        if (_sampler is null || data is null)
        {
            return;
        }
        MarkInvited();

        var width = PlotWidth;

        if (x < _downX || x > _upX)
        {
            // outside the night: the reader is awake, and rides the horizon
            var clamped = Math.Clamp(x, EdgeInset, width - EdgeInset);

            ShowScrub(new Point(clamped, HorizonY), showLine: false);
            Announce(clamped / width, $"{Format.Clock(data.At(clamped / width))} — awake");
            return;
        }

        var point = _sampler.PointAtX(x);
        var depth = (point.Y - HorizonY) / Depth;

        ShowScrub(point, showLine: true);
        Announce(
            point.X / width,
            $"{Format.Clock(data.At(point.X / width))} — {DepthWord(depth)} sleep");
    }

    /// <summary>
    /// One reading, said twice: to the eye through the caption, and to the
    /// machine as a range value plus the text that gives it meaning. UIA has no
    /// direct counterpart to aria-valuetext, so the words ride on help text -
    /// the one property a screen reader re-reads alongside a changed value.
    /// </summary>
    void Announce(double fraction, string text)
    {
        var previous = _value;

        _value = Math.Clamp(fraction, 0, 1);
        _valueText = text;

        Publish(Reading.Preview(text));

        if (FrameworkElementAutomationPeer.FromElement(this) is not { } peer)
        {
            return;
        }

        try
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, previous, _value);
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, string.Empty, text);
        }
        catch (NotImplementedException)
        {
            // not every target implements every identifier; the caption still speaks
        }
    }

    void ShowScrub(Point at, bool showLine)
    {
        if (_scrubDot is not null)
        {
            Place(_scrubDot, at.X - _scrubDot.Width / 2, at.Y - _scrubDot.Height / 2);
            _scrubDot.Opacity = 1;
        }

        if (_scrubLine is not null)
        {
            _scrubLine.X1 = _scrubLine.X2 = at.X;
            _scrubLine.Opacity = showLine ? 1 : 0;
        }
    }

    void EndScrub()
    {

        if (!_scrubbing && _keyX is null)
        {
            return;
        }

        _scrubbing = false;
        _keyX = null;
        _valueText = string.Empty;

        if (_scrubDot is not null)
        {
            _scrubDot.Opacity = 0;
        }

        if (_scrubLine is not null)
        {
            _scrubLine.Opacity = 0;
        }

        PublishDefault();
    }

    /// <summary>
    /// The night as a slider. Min and max are the ends of the plot rather than
    /// the ends of the sleep, because the reader can wander past either edge of
    /// the night and the chart still has something to say there.
    /// </summary>
    sealed class NightAutomationPeer(NightChart owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => 1;

        public double SmallChange => KeyStep;

        public double LargeChange => KeyStep * 5;

        public double Value => ((NightChart)Owner).Value;

        public void SetValue(double value) => ((NightChart)Owner).ScrubToFraction(value);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => nameof(NightChart);

        /// <summary>The aria-valuetext stand-in: what the current value means.</summary>
        protected override string GetHelpTextCore()
        {
            var owner = (NightChart)Owner;

            return string.IsNullOrEmpty(owner.ValueText)
                ? base.GetHelpTextCore() ?? string.Empty
                : owner.ValueText;
        }

        protected override object GetPatternCore(PatternInterface pattern)
        {
            if (pattern == PatternInterface.RangeValue)
            {
                return this;
            }

            // the base returns null for patterns it does not serve, whatever
            // the signature's annotation says
            return base.GetPatternCore(pattern)!;
        }
    }
}
