using Measures.Motion;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace Measures.Controls;

/// <summary>
/// A mark on a chart that is also a real focus stop. The hit area is far larger
/// than the drawn dot, so a fingertip finds it; the dot itself changes size
/// through a scale transform rather than its radius, because scale composites
/// while width and height re-layout.
/// </summary>
public sealed partial class DotButton : ButtonBase
{
    public static readonly DependencyProperty HitSizeProperty = DependencyProperty.Register(
        nameof(HitSize), typeof(double), typeof(DotButton), new PropertyMetadata(28d));

    public static readonly DependencyProperty BaseSizeProperty = DependencyProperty.Register(
        nameof(BaseSize), typeof(double), typeof(DotButton), new PropertyMetadata(10d));

    public static readonly DependencyProperty FocusSizeProperty = DependencyProperty.Register(
        nameof(FocusSize), typeof(double), typeof(DotButton), new PropertyMetadata(16d));

    /// <summary>The house duration for a mark changing size.</summary>
    const double GrowMs = 300;

    readonly SolidColorBrush _fill = new();
    readonly SolidColorBrush _stroke = new();
    readonly ScaleTransform _scale = new() { ScaleX = 1, ScaleY = 1 };

    Ellipse? _mark;
    Ellipse? _focus;
    Storyboard? _scaleX;
    Storyboard? _scaleY;
    IDisposable? _breath;
    double _radius;
    double _strokeThickness = 1.5;

    public DotButton()
    {
        DefaultStyleKey = typeof(DotButton);

        // :focus-visible, not :focus - a pointer press leaves no ring
        GotFocus += (_, _) => ShowFocus(FocusState == FocusState.Keyboard);
        LostFocus += (_, _) => ShowFocus(false);
        Unloaded += (_, _) =>
        {
            _breath?.Dispose();
            StopRadius();
        };
    }

    /// <summary>Diameter of the transparent hit target.</summary>
    public double HitSize
    {
        get => (double)GetValue(HitSizeProperty);
        set => SetValue(HitSizeProperty, value);
    }

    /// <summary>Diameter the dot is laid out at; every radius change scales from here.</summary>
    public double BaseSize
    {
        get => (double)GetValue(BaseSizeProperty);
        set => SetValue(BaseSizeProperty, value);
    }

    public double FocusSize
    {
        get => (double)GetValue(FocusSizeProperty);
        set => SetValue(FocusSizeProperty, value);
    }

    /// <summary>The centre of the dot, in the coordinates of its parent canvas.</summary>
    public Point Centre { get; private set; }

    /// <summary>
    /// True while the shared clock owns this mark's size. Hover growth has to
    /// leave it alone: two writers on one scale transform is a fight the breath
    /// wins every tick, and the mark just judders.
    /// </summary>
    public bool IsBreathing => _breath is not null;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _mark = GetTemplateChild("PART_Mark") as Ellipse;
        _focus = GetTemplateChild("PART_Focus") as Ellipse;

        if (_mark is not null)
        {
            _mark.Fill = _fill;
            _mark.Stroke = _stroke;
            _mark.StrokeThickness = _strokeThickness;
            _mark.RenderTransform = _scale;
            _mark.RenderTransformOrigin = new Point(0.5, 0.5);
            _scale.ScaleX = _scale.ScaleY = _radius <= 0 ? 1 : _radius / (BaseSize / 2);
        }

        if (_focus is not null)
        {
            _focus.Opacity = 0;
        }
    }

    /// <summary>
    /// The footfall settles into place as the road reaches it. The initial
    /// values are set now, not by the animation: with a delay, the element
    /// would render at rest for a beat before jumping back to its start.
    /// </summary>
    public void PlayEntrance(double delayMs)
    {
        var shift = new TranslateTransform { Y = -5 };
        RenderTransform = shift;
        Opacity = 0;

        Anim.Animate(shift, "Y", -5, 0, 350, Anim.EaseGravity, delayMs);
        Anim.Animate(this, "Opacity", 0, 1, 350, Anim.EaseGravity, delayMs);
    }

    public void MoveTo(double centreX, double centreY)
    {
        Centre = new Point(centreX, centreY);

        Canvas.SetLeft(this, centreX - HitSize / 2);
        Canvas.SetTop(this, centreY - HitSize / 2);
    }

    /// <summary>Radius in chart units. Animated over the house 300 ms.</summary>
    public void SetRadius(double radius, bool animate = true) =>
        SetRadius(radius, animate ? GrowMs : 0);

    /// <summary>Radius in chart units, over a given duration. Zero lands it at once.</summary>
    public void SetRadius(double radius, double ms)
    {
        _radius = radius;

        var target = radius / (BaseSize / 2);

        // a HoldEnd storyboard keeps overriding local values after it finishes,
        // so the previous size animation has to be released before either path
        StopRadius();

        if (ms <= 0 || !Anim.Enabled)
        {
            _scale.ScaleX = _scale.ScaleY = target;
            return;
        }

        _scaleX = Anim.Animate(_scale, "ScaleX", null, target, ms, Anim.EaseHands);
        _scaleY = Anim.Animate(_scale, "ScaleY", null, target, ms, Anim.EaseHands);
    }

    public void SetColours(Color fill, Color stroke, double thickness, bool animate = true)
    {
        _strokeThickness = thickness;

        if (_mark is not null)
        {
            _mark.StrokeThickness = thickness;
        }

        if (!animate)
        {
            _fill.Color = fill;
            _stroke.Color = stroke;
            return;
        }

        Anim.AnimateColor(_fill, "Color", fill, 300);
        Anim.AnimateColor(_stroke, "Color", stroke, 300);
    }

    /// <summary>
    /// The today-mark breathes between two radii, off the shared clock. Turning
    /// it off is pure teardown - the caller sets the radius it wants next, so
    /// this never fights an incoming size.
    /// </summary>
    public void SetBreathing(bool on, double from = 3.5, double to = 5)
    {
        _breath?.Dispose();
        _breath = null;

        if (!on)
        {
            return;
        }

        StopRadius();
        _radius = from;
        _breath = BreathClock.BreatheRadius(_scale, from / (BaseSize / 2), to / (BaseSize / 2));
    }

    void StopRadius()
    {
        _scaleX?.Stop();
        _scaleY?.Stop();
        _scaleX = null;
        _scaleY = null;
    }

    void ShowFocus(bool visible)
    {
        if (_focus is not null)
        {
            _focus.Opacity = visible ? 1 : 0;
        }
    }
}
