using Measures.Motion;
using Measures.Theming;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace Measures.Controls;

/// <summary>
/// One day of rain: a stem you can pour into, and detach a drop from. The drop
/// falls its own true height, so a heavy day takes visibly longer to land than
/// a light one - the chart is read as much by ear as by eye.
/// </summary>
public sealed partial class StemButton : ButtonBase
{
    public static readonly DependencyProperty StemHeightProperty = DependencyProperty.Register(
        nameof(StemHeight), typeof(double), typeof(StemButton), new PropertyMetadata(0d, OnStemHeightChanged));

    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(StemButton), new PropertyMetadata(false, OnIsOnChanged));

    const double RowHeight = 92;
    const double DropSize = 3.5;
    const double BeadReformMs = 450;
    const double BeadDelayMs = 700;

    readonly SolidColorBrush _body = new();
    readonly ScaleTransform _pourScale = new() { ScaleY = 0 };
    readonly TranslateTransform _dropShift = new();
    readonly ScaleTransform _splashScale = new() { ScaleX = 1, ScaleY = 1 };
    readonly ScaleTransform _beadScale = new() { ScaleX = 1, ScaleY = 1 };

    FrameworkElement? _stem;
    Border? _bodyPart;
    Border? _pour;
    Ellipse? _bead;
    Ellipse? _drop;
    Ellipse? _splash;
    Rectangle? _focus;
    IDisposable? _breath;
    bool _hasBead;

    public StemButton()
    {
        DefaultStyleKey = typeof(StemButton);

        PointerEntered += (_, _) => Pour(true);
        PointerExited += (_, _) => Pour(false);
        GotFocus += OnGotFocus;
        LostFocus += (_, _) => { Pour(false); ShowFocus(false); };

        Palette.Recoloured += OnRecoloured;
        Unloaded += (_, _) =>
        {
            Palette.Recoloured -= OnRecoloured;
            _breath?.Dispose();
        };
    }

    /// <summary>Drawn height of the stem in pixels.</summary>
    public double StemHeight
    {
        get => (double)GetValue(StemHeightProperty);
        set => SetValue(StemHeightProperty, value);
    }

    /// <summary>Indigo rather than ink: this day is the one being read.</summary>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _stem = GetTemplateChild("PART_Stem") as FrameworkElement;
        _bodyPart = GetTemplateChild("PART_Body") as Border;
        _pour = GetTemplateChild("PART_Pour") as Border;
        _bead = GetTemplateChild("PART_Bead") as Ellipse;
        _drop = GetTemplateChild("PART_Drop") as Ellipse;
        _splash = GetTemplateChild("PART_Splash") as Ellipse;
        _focus = GetTemplateChild("PART_Focus") as Rectangle;

        if (_bodyPart is not null)
        {
            _bodyPart.Background = _body;
        }

        if (_pour is not null)
        {
            _pour.RenderTransform = _pourScale;
            _pour.RenderTransformOrigin = new Point(0.5, 0);
        }

        if (_drop is not null)
        {
            _drop.RenderTransform = _dropShift;
            _drop.Opacity = 0;
        }

        if (_splash is not null)
        {
            _splash.RenderTransform = _splashScale;
            _splash.RenderTransformOrigin = new Point(0.5, 0.5);
            _splash.Opacity = 0;
        }

        if (_bead is not null)
        {
            _bead.RenderTransform = _beadScale;
            _bead.RenderTransformOrigin = new Point(0.5, 0.5);
            _bead.Visibility = _hasBead ? Visibility.Visible : Visibility.Collapsed;
        }

        ApplyHeight();
        ApplyOn(animate: false);
        ApplyBead();
    }

    /// <summary>Only today carries a bead, and only while it has not just been shed.</summary>
    public void SetBead(bool has)
    {
        if (_hasBead == has)
        {
            return;
        }

        _hasBead = has;
        ApplyBead();
    }

    /// <summary>The rainfall entrance: the stem arrives from above, under gravity.</summary>
    public void PlayEntrance(double delayMs)
    {
        if (_stem is null)
        {
            return;
        }

        var shift = new TranslateTransform();
        _stem.RenderTransform = shift;

        Anim.Animate(shift, "Y", -46, 0, 600, Anim.Gravity, delayMs);
        Anim.Animate(_stem, "Opacity", 0, 1, 330, Anim.Gravity, delayMs);
    }

    /// <summary>
    /// Detaches a drop and lets it fall the stem's true height. Duration goes
    /// with the square root of the distance, the way a real fall does.
    /// </summary>
    public void Shed()
    {
        if (_drop is null || StemHeight <= 0 || !Anim.Enabled)
        {
            Splash();
            return;
        }

        var fall = StemHeight;
        var seconds = 0.22 + Math.Sqrt(StemHeight / RowHeight) * 0.30;

        if (_hasBead)
        {
            HideBead();
        }

        _drop.Opacity = 1;
        Anim.Animate(_dropShift, "Y", 0, fall, seconds * 1000, Anim.Gravity, completed: () =>
        {
            _drop.Opacity = 0;
            _dropShift.Y = 0;
            Splash();
        });
    }

    static void OnStemHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StemButton)d).ApplyHeight();

    static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StemButton)d).ApplyOn(animate: true);

    void ApplyHeight()
    {
        var height = Math.Max(StemHeight, 0);

        if (_bodyPart is not null)
        {
            _bodyPart.Height = height;
        }

        if (_pour is not null)
        {
            _pour.Height = height;
        }
    }

    void ApplyOn(bool animate)
    {
        var colour = IsOn ? Palette.Get(Palette.Token.Ai) : Palette.Get(Palette.Token.Ink, 0.35);

        if (animate)
        {
            Anim.AnimateColor(_body, "Color", colour, 300);
        }
        else
        {
            _body.Color = colour;
        }
    }

    void ApplyBead()
    {
        _breath?.Dispose();
        _breath = null;

        if (_bead is null)
        {
            return;
        }

        _bead.Visibility = _hasBead ? Visibility.Visible : Visibility.Collapsed;

        if (!_hasBead)
        {
            return;
        }

        _bead.Opacity = 1;
        _beadScale.ScaleX = _beadScale.ScaleY = 1;
        _breath = BreathClock.Breathe(_bead, _beadScale);
    }

    void HideBead()
    {
        _breath?.Dispose();
        _breath = null;

        if (_bead is not null)
        {
            _bead.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>A flat landing: the drop spreads sideways and is gone.</summary>
    void Splash()
    {
        if (_splash is not null && Anim.Enabled)
        {
            _splash.Opacity = 0.9;
            _splashScale.ScaleX = 1;

            Anim.Animate(_splashScale, "ScaleX", 1, 3.2, 400, Anim.EaseOut);
            Anim.Animate(_splash, "Opacity", 0.9, 0, 400, Anim.EaseOut);
        }

        if (!_hasBead)
        {
            return;
        }

        // a new bead gathers a beat after the old one lands
        var reform = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(BeadDelayMs) };
        reform.Tick += (_, _) =>
        {
            reform.Stop();
            ReformBead();
        };
        reform.Start();
    }

    void ReformBead()
    {
        if (_bead is null || !_hasBead)
        {
            return;
        }

        _bead.Visibility = Visibility.Visible;
        _bead.Opacity = 1;

        Anim.Animate(_beadScale, "ScaleX", 0, 1, BeadReformMs, Anim.EaseOut);
        Anim.Animate(_beadScale, "ScaleY", 0, 1, BeadReformMs, Anim.EaseOut, completed: ApplyBead);
    }

    /// <summary>
    /// The pour fills from the top down under gravity and retracts by hand -
    /// the page law, applied to a hover.
    /// </summary>
    void Pour(bool on) =>
        Anim.Animate(_pourScale, "ScaleY", null, on ? 1 : 0, 300, on ? Anim.Gravity : Anim.Hands);

    /// <summary>
    /// Only keyboard focus paints the ring and pours the stem, matching
    /// :focus-visible - a pointer press should leave no residue.
    /// </summary>
    void OnGotFocus(object sender, RoutedEventArgs e)
    {
        var visible = FocusState == FocusState.Keyboard;

        if (visible)
        {
            Pour(true);
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

    void OnRecoloured(object? sender, EventArgs e) => ApplyOn(animate: false);
}
