using System.Diagnostics;
using Measures.Motion;
using Measures.Theming;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace Measures.Controls;

/// <summary>
/// One day of sessions, as a stack of beads. A session is a discrete countable
/// thing, so the chart is the count: the beads are uniform in size and colour,
/// because grading either would encode the same fact twice and undo the
/// count-the-drops premise.
/// </summary>
public sealed partial class BeadColumn : ButtonBase
{
    /// <summary>Beads per day above which a single column stops being subitizable.</summary>
    public const int Ceiling = 6;

    const double BeadSize = 4.5;
    const double StackGap = 3;
    const double Pitch = BeadSize + StackGap;
    const double RestRingSize = 5;

    const double FallMs = 620;
    const double BeadStaggerMs = 110;

    /// <summary>How far above the plot a bead starts, so it arrives from the sky.</summary>
    const double SkyOffset = 130;

    const double HopHeight = -7;
    const double HopMs = 300;
    const double PlipMs = 400;
    const double PlipDelayMs = 260;
    const double CascadeStepMs = 45;

    readonly List<Ellipse> _beads = [];
    readonly List<SolidColorBrush> _fills = [];
    readonly List<Storyboard> _running = [];

    readonly TranslateTransform _hop = new();
    readonly ScaleTransform _plipScale = new() { ScaleX = 0.6, ScaleY = 0.6 };

    Canvas? _host;
    Rectangle? _focus;
    Ellipse? _rest;
    Ellipse? _plip;
    IDisposable? _breath;
    int _count;
    bool _isOn;
    bool _breathes;
    double? _pendingEntrance;

    public BeadColumn()
    {
        DefaultStyleKey = typeof(BeadColumn);

        PointerEntered += (_, _) => Cascade(true);
        PointerExited += (_, _) => Cascade(false);
        GotFocus += OnGotFocus;
        LostFocus += (_, _) => { Cascade(false); ShowFocus(false); };

        Palette.Recoloured += OnRecoloured;
        Unloaded += (_, _) =>
        {
            Palette.Recoloured -= OnRecoloured;
            _breath?.Dispose();
            StopAll();
        };
    }

    /// <summary>Sessions on this day. Zero draws a rest ring instead of beads.</summary>
    public int Count
    {
        get => _count;
        set
        {
            _count = Math.Max(value, 0);

            // a column taller than the ceiling stops being countable at a glance.
            // Written rather than asserted: a modal assert dialog would freeze
            // the render thread of a running app.
            if (_count > Ceiling)
            {
                Debug.WriteLine($"BeadColumn: {_count} sessions exceeds the bead ceiling of {Ceiling}.");
            }

            Build();
        }
    }

    /// <summary>Indigo rather than ink: this day is the one being read.</summary>
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
            {
                return;
            }

            _isOn = value;
            Recolour(animate: true);
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _host = GetTemplateChild("PART_Beads") as Canvas;
        _focus = GetTemplateChild("PART_Focus") as Rectangle;
        _plip = GetTemplateChild("PART_Plip") as Ellipse;

        if (_plip is not null)
        {
            _plip.RenderTransform = _plipScale;
            _plip.RenderTransformOrigin = new Point(0.5, 0.5);
            _plip.Opacity = 0;
        }

        Build();
    }

    /// <summary>The top bead is the living edge: only today's, and only when nothing else is read.</summary>
    public void SetBreathingTop(bool on)
    {
        if (_breathes == on)
        {
            return;
        }

        _breathes = on;
        ApplyBreath();
    }

    /// <summary>
    /// The day rains in, bottom bead first. The request is remembered rather
    /// than run on the spot: a column added to the tree has not necessarily had
    /// its template applied yet, so the beads it is meant to drop may not exist
    /// for another layout pass. Build runs whatever is still pending.
    /// </summary>
    public void PlayEntrance(double delayMs)
    {
        if (!Anim.Enabled)
        {
            return;
        }

        _pendingEntrance = delayMs;
        RunPendingEntrance();
    }

    void RunPendingEntrance()
    {
        if (_pendingEntrance is not { } delayMs)
        {
            return;
        }

        // an empty day has no beads to drop, but it still has to arrive with
        // the rest of the fortnight rather than sit there waiting for it
        if (_rest is not null)
        {
            _pendingEntrance = null;
            _rest.Opacity = 0;
            Track(Anim.Animate(_rest, "Opacity", 0, 1, 400, Anim.Ease, delayMs));
            return;
        }

        if (_beads.Count == 0)
        {
            return;
        }

        _pendingEntrance = null;

        for (var i = 0; i < _beads.Count; i++)
        {
            // each bead comes in from well above the card and drops into its
            // slot, so the column visibly stacks: the bottom bead falls furthest
            // and lands first, and every one after it settles onto the pile
            var rest = Canvas.GetTop(_beads[i]);
            var from = -(rest + BeadSize + SkyOffset);
            var fall = Math.Abs(from);

            // one gravity for all of them: time goes with the square root of the
            // distance, the same law the droplet fell under
            var ms = FallMs * Math.Sqrt(fall / (Height + SkyOffset));

            var shift = new TranslateTransform { Y = from };
            _beads[i].RenderTransform = shift;
            _beads[i].Opacity = 0;

            var begin = delayMs + i * BeadStaggerMs;

            Track(Anim.Animate(shift, "Y", from, 0, ms, Anim.Gravity, begin));
            // in before it is halfway down, so it is a falling bead and not a
            // bead that materialises at the moment it lands
            Track(Anim.Animate(_beads[i], "Opacity", 0, 1, ms * 0.25, Anim.Gravity, begin));
        }
    }

    /// <summary>
    /// The plip: the top bead hops and lands, and a ring spreads from where it
    /// fell. Restarting mid-flight is safe - the previous run is stopped and
    /// its transforms reset, so rapid taps leave nothing orphaned.
    /// </summary>
    public void Plip()
    {
        if (!Anim.Enabled || _beads.Count == 0)
        {
            return;
        }

        StopAll();

        var top = _beads[^1];
        top.RenderTransform = _hop;
        _hop.Y = 0;

        Track(Anim.Animate(_hop, "Y", 0, HopHeight, HopMs / 2, Anim.Hands, completed: () =>
            Track(Anim.Animate(_hop, "Y", HopHeight, 0, HopMs / 2, Anim.Gravity))));

        if (_plip is null)
        {
            return;
        }

        PlacePlip();

        _plipScale.ScaleX = _plipScale.ScaleY = 0.6;
        _plip.Opacity = 0;

        Track(Anim.Animate(_plipScale, "ScaleX", 0.6, 2.4, PlipMs, Anim.EaseOut, PlipDelayMs));
        Track(Anim.Animate(_plipScale, "ScaleY", 0.6, 2.4, PlipMs, Anim.EaseOut, PlipDelayMs));
        Track(Anim.Animate(_plip, "Opacity", 0.9, 0, PlipMs, Anim.EaseOut, PlipDelayMs));
    }

    /// <summary>
    /// Hover and focus wash the column indigo from the top down, one bead at a
    /// time - the same downward read the gradient pour used to give, in the
    /// discrete form the beads ask for.
    /// </summary>
    void Cascade(bool on)
    {
        if (_isOn)
        {
            return;
        }

        var target = on ? Palette.Get(Palette.Token.Ai) : Palette.Get(Palette.Token.Ink, 0.75);

        for (var i = 0; i < _fills.Count; i++)
        {
            // index 0 is the bottom bead, so the top one leads
            var fromTop = _fills.Count - 1 - i;
            var delay = Anim.Enabled ? fromTop * CascadeStepMs : 0;

            Anim.AnimateColor(_fills[i], "Color", target, 160, delay);
        }
    }

    void Build()
    {
        if (_host is null)
        {
            return;
        }

        _breath?.Dispose();
        _breath = null;
        StopAll();

        _host.Children.Clear();
        _beads.Clear();
        _fills.Clear();

        var centreX = Width / 2;
        var floor = Height;

        if (_count == 0)
        {
            // a day with nothing on it still says so
            _rest = new Ellipse
            {
                Width = RestRingSize,
                Height = RestRingSize,
                Fill = null,
                Stroke = new SolidColorBrush(Palette.Get(Palette.Token.Ink, 0.35)),
                StrokeThickness = 1,
            };

            Canvas.SetLeft(_rest, centreX - RestRingSize / 2);
            Canvas.SetTop(_rest, floor - RestRingSize);
            _host.Children.Add(_rest);
            return;
        }

        _rest = null;

        for (var i = 0; i < _count; i++)
        {
            var fill = new SolidColorBrush(BaseColour());
            var bead = new Ellipse
            {
                Width = BeadSize,
                Height = BeadSize,
                Fill = fill,
            };

            Canvas.SetLeft(bead, centreX - BeadSize / 2);
            Canvas.SetTop(bead, floor - BeadSize - i * Pitch);

            _host.Children.Add(bead);
            _beads.Add(bead);
            _fills.Add(fill);
        }

        PlacePlip();
        ApplyBreath();
        RunPendingEntrance();
    }

    void PlacePlip()
    {
        if (_plip is null || _beads.Count == 0)
        {
            return;
        }

        var top = _beads[^1];

        Canvas.SetLeft(_plip, Canvas.GetLeft(top) + BeadSize / 2 - _plip.Width / 2);
        Canvas.SetTop(_plip, Canvas.GetTop(top) + BeadSize / 2 - _plip.Height / 2);
    }

    Color BaseColour() => _isOn ? Palette.Get(Palette.Token.Ai) : Palette.Get(Palette.Token.Ink, 0.75);

    void Recolour(bool animate)
    {
        var colour = BaseColour();

        foreach (var fill in _fills)
        {
            if (animate)
            {
                Anim.AnimateColor(fill, "Color", colour, 300);
            }
            else
            {
                fill.Color = colour;
            }
        }

        if (_rest?.Stroke is SolidColorBrush ring)
        {
            ring.Color = Palette.Get(Palette.Token.Ink, 0.35);
        }
    }

    void ApplyBreath()
    {
        _breath?.Dispose();
        _breath = null;

        if (_beads.Count == 0)
        {
            return;
        }

        var top = _beads[^1];

        if (!_breathes)
        {
            // the breath writes opacity and scale every tick; stopping it has to
            // put both back, or the bead keeps whatever the last tick left
            top.Opacity = 1;
            top.RenderTransform = null;
            return;
        }

        var scale = new ScaleTransform();
        top.RenderTransform = scale;
        top.RenderTransformOrigin = new Point(0.5, 0.5);

        _breath = BreathClock.Breathe(top, scale);
    }

    void Track(Storyboard board) => _running.Add(board);

    void StopAll()
    {
        foreach (var board in _running)
        {
            board.Stop();
        }

        _running.Clear();
        _hop.Y = 0;

        if (_plip is not null)
        {
            _plip.Opacity = 0;
        }
    }

    void OnGotFocus(object sender, RoutedEventArgs e)
    {
        // :focus-visible, not :focus - a pointer press leaves no ring
        var visible = FocusState == FocusState.Keyboard;

        if (visible)
        {
            Cascade(true);
        }

        ShowFocus(visible);
    }

    void ShowFocus(bool visible)
    {
        if (_focus is not null)
        {
            _focus.Opacity = visible ? 1 : 0;
        }
    }

    void OnRecoloured(object? sender, EventArgs e) => Recolour(animate: false);
}
