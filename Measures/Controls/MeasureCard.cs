using Measures.Motion;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Measures.Controls;

/// <summary>
/// The device face every measure sits in. It owns all the chrome, so the four
/// charts only ever draw their plot: numeral tops and chart bottoms are the two
/// horizontals shared across the bench, and they line up because this control
/// decides them, not the charts.
///
/// It is also the thing the page is arranged around - pick it up by its chrome
/// and move it, and the glyph field behind parts to make room.
/// </summary>
public sealed partial class MeasureCard : ContentControl, IFieldSource
{
    public static readonly DependencyProperty HeadLeftProperty = DependencyProperty.Register(
        nameof(HeadLeft), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeadRightProperty = DependencyProperty.Register(
        nameof(HeadRight), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FootRightProperty = DependencyProperty.Register(
        nameof(FootRight), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NumeralValueProperty = DependencyProperty.Register(
        nameof(NumeralValue), typeof(string), typeof(MeasureCard), new PropertyMetadata("—", OnNumeralChanged));

    public static readonly DependencyProperty NumeralSuffixProperty = DependencyProperty.Register(
        nameof(NumeralSuffix), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty, OnNumeralChanged));

    public static readonly DependencyProperty NumeralTailProperty = DependencyProperty.Register(
        nameof(NumeralTail), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty, OnNumeralChanged));

    /// <summary>Comma separated axis whispers, laid out edge to edge under the chart.</summary>
    public static readonly DependencyProperty AxisLabelsProperty = DependencyProperty.Register(
        nameof(AxisLabels), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty, OnAxisChanged));

    public static readonly DependencyProperty RiseDelayProperty = DependencyProperty.Register(
        nameof(RiseDelay), typeof(double), typeof(MeasureCard), new PropertyMetadata(0d));

    readonly TranslateTransform _rise = new();
    readonly TranslateTransform _drag = new();

    Pointer? _held;
    UIElement? _space;
    Point _grab;
    double _grabX;
    double _grabY;
    double _flingX;
    double _flingY;
    DispatcherTimer? _coast;

    FrameworkElement? _riseHost;
    TextBlock? _numeral;
    Canvas? _axis;
    ICaptionSource? _source;
    bool _loaded;
    bool _risen;

    public MeasureCard()
    {
        DefaultStyleKey = typeof(MeasureCard);

        RenderTransform = _drag;

        // plain handlers, so an instrument that has already claimed the press
        // for a pin or a scrub keeps it - the card is picked up by its chrome
        PointerPressed += OnDragStart;
        PointerMoved += OnDragMove;
        PointerReleased += OnDragEnd;
        PointerCaptureLost += OnDragEnd;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    public event EventHandler? Moved;

    /// <inheritdoc />
    public event EventHandler? Released;

    public string HeadLeft
    {
        get => (string)GetValue(HeadLeftProperty);
        set => SetValue(HeadLeftProperty, value);
    }

    public string HeadRight
    {
        get => (string)GetValue(HeadRightProperty);
        set => SetValue(HeadRightProperty, value);
    }

    public string FootRight
    {
        get => (string)GetValue(FootRightProperty);
        set => SetValue(FootRightProperty, value);
    }

    public string NumeralValue
    {
        get => (string)GetValue(NumeralValueProperty);
        set => SetValue(NumeralValueProperty, value);
    }

    public string NumeralSuffix
    {
        get => (string)GetValue(NumeralSuffixProperty);
        set => SetValue(NumeralSuffixProperty, value);
    }

    public string NumeralTail
    {
        get => (string)GetValue(NumeralTailProperty);
        set => SetValue(NumeralTailProperty, value);
    }

    public string AxisLabels
    {
        get => (string)GetValue(AxisLabelsProperty);
        set => SetValue(AxisLabelsProperty, value);
    }

    /// <summary>Stagger for the entrance, in milliseconds.</summary>
    public double RiseDelay
    {
        get => (double)GetValue(RiseDelayProperty);
        set => SetValue(RiseDelayProperty, value);
    }

    public CaptionPresenter? Caption { get; private set; }

    /// <summary>Line box for the axis row, at the 8.5 epx whisper size.</summary>
    const double AxisHeight = 12;

    /// <summary>How much of the card must stay on screen, however hard it is thrown.</summary>
    const double EdgeKeep = 96;

    /// <summary>
    /// Momentum bleed per 60 fps frame after the hand lets go. At 0.92 a throw
    /// coasts about twelve times its last frame of travel and is still inside
    /// 600 ms - weight, without the card sailing away.
    /// </summary>
    const double CoastDamp = 0.92;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _riseHost = GetTemplateChild("PART_Rise") as FrameworkElement;
        _numeral = GetTemplateChild("PART_Numeral") as TextBlock;
        _axis = GetTemplateChild("PART_Axis") as Canvas;

        if (_axis is not null)
        {
            _axis.SizeChanged += (_, _) => PlaceAxis();
        }
        Caption = GetTemplateChild("PART_Caption") as CaptionPresenter;

        if (_riseHost is not null && !_risen)
        {
            _riseHost.RenderTransform = _rise;
            _riseHost.Opacity = 0;
            _rise.Y = 14;
        }

        BuildNumeral();
        BuildAxis();
        AttachSource();
        TryRise();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (oldContent is ICaptionSource previous)
        {
            previous.ReadingChanged -= OnReadingChanged;
        }

        _source = null;
        AttachSource();
    }

    static void OnNumeralChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MeasureCard)d).BuildNumeral();

    static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MeasureCard)d).BuildAxis();

    void AttachSource()
    {
        if (_source is not null || Content is not ICaptionSource source)
        {
            return;
        }

        _source = source;
        source.ReadingChanged += OnReadingChanged;
    }

    void OnReadingChanged(object? sender, Reading reading)
    {
        if (Caption is null)
        {
            return;
        }

        if (reading.Immediate)
        {
            Caption.SetPreview(reading.Text, reading.Live);
        }
        else if (reading.Live)
        {
            Caption.SetReading(reading.Text, live: true);
        }
        else
        {
            Caption.SetDefault(reading.Text);
        }
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        TryRise();

        _source?.PublishInitialReading();
    }

    /// <summary>
    /// Loaded and OnApplyTemplate do not have a guaranteed order here, and a
    /// card whose entrance was scheduled against a template that had not
    /// arrived yet stays at opacity zero forever. Whichever lands last starts
    /// the rise, and only once.
    /// </summary>
    void TryRise()
    {
        if (_risen || !_loaded || _riseHost is null)
        {
            return;
        }

        _risen = true;

        Anim.Animate(_riseHost, "Opacity", 0, 1, 900, Anim.Hands, RiseDelay);
        Anim.Animate(_rise, "Y", 14, 0, 900, Anim.Hands, RiseDelay);
    }

    /// <summary>
    /// Asks the instrument to draw itself in again. The card itself does not
    /// rise a second time: the rise is an arrival, and the cards have already
    /// arrived - replaying it would just make the page flinch.
    /// </summary>
    public void Replay() => _source?.Replay();

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopCoast();

        if (_source is not null)
        {
            _source.ReadingChanged -= OnReadingChanged;
            _source = null;
        }
    }

    /// <summary>
    /// The numeral is one line of three weights: the figure, a quiet italic
    /// aside, and sometimes a trailing figure ("7 h 32"). Built in code because
    /// named Runs inside a template are not reliably reachable.
    /// </summary>
    void BuildNumeral()
    {
        if (_numeral is null)
        {
            return;
        }

        _numeral.Inlines.Clear();

        _numeral.Inlines.Add(new Run { Text = NumeralValue ?? string.Empty });

        if (!string.IsNullOrEmpty(NumeralSuffix))
        {
            _numeral.Inlines.Add(new Run
            {
                Text = NumeralSuffix,
                FontSize = (double)Application.Current.Resources["NumeralSoftSize"],
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = (Brush)Application.Current.Resources["QuietBrush"],
            });
        }

        if (!string.IsNullOrEmpty(NumeralTail))
        {
            _numeral.Inlines.Add(new Run { Text = NumeralTail });
        }
    }

    /// <summary>
    /// Axis whispers are anchored to the fraction of the chart they describe,
    /// not spread evenly: on the night card those are clock hours, and an "06"
    /// sitting at 70% of the night points at the wrong time.
    /// </summary>
    void BuildAxis()
    {
        if (_axis is null)
        {
            return;
        }

        _axis.Children.Clear();

        var labels = (AxisLabels ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var label in labels)
        {
            _axis.Children.Add(new SpacedText
            {
                Text = label,
                Style = (Style)Application.Current.Resources["AxisStyle"],
            });
        }

        _axis.Height = labels.Length == 0 ? 0 : AxisHeight;
        PlaceAxis();
    }

    void PlaceAxis()
    {
        if (_axis is null || _axis.Children.Count == 0)
        {
            return;
        }

        var width = _axis.ActualWidth;

        if (width <= 0)
        {
            return;
        }

        var last = _axis.Children.Count - 1;

        for (var i = 0; i <= last; i++)
        {
            var label = (FrameworkElement)_axis.Children[i];

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // the ends sit flush, everything between is centred on its tick
            var fraction = last == 0 ? 0 : (double)i / last;

            Canvas.SetLeft(label, fraction * width - label.DesiredSize.Width * fraction);
        }
    }

    /// <summary>
    /// The card is picked up by its chrome. A press that started inside the
    /// instrument belongs to the instrument - a pin, a bead, a scrub - so the
    /// plot is never a drag handle, whether or not the chart marked it handled.
    /// </summary>
    void OnDragStart(object sender, PointerRoutedEventArgs e)
    {
        if (_held is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (Content is DependencyObject instrument && StartedInside(e.OriginalSource, instrument))
        {
            return;
        }

        StopCoast();

        if (!CapturePointer(e.Pointer))
        {
            return;
        }

        _held = e.Pointer;

        // Measured against the window root, which is the one element that
        // neither this card nor the pointer can move. Passing null here reads
        // as the sender's own space on Uno, and the sender changes to whichever
        // child is under the pointer mid-drag, which throws the card across the
        // page. Never null.
        _space = Space;
        _grab = e.GetCurrentPoint(_space).Position;
        _grabX = _drag.X;
        _grabY = _drag.Y;
        _flingX = 0;
        _flingY = 0;

        e.Handled = true;
    }

    void OnDragMove(object sender, PointerRoutedEventArgs e)
    {
        if (_held is null || e.Pointer.PointerId != _held.PointerId)
        {
            return;
        }

        var now = e.GetCurrentPoint(_space).Position;
        var x = _grabX + (now.X - _grab.X);
        var y = _grabY + (now.Y - _grab.Y);

        Clamp(ref x, ref y);

        MoveTo(x, y);

        e.Handled = true;
    }

    void OnDragEnd(object sender, PointerRoutedEventArgs e)
    {
        if (_held is null)
        {
            return;
        }

        var pointer = _held;
        _held = null;
        _space = null;

        ReleasePointerCapture(pointer);
        StartCoast();

        Released?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Direct manipulation is 1:1 and unsmoothed; the lag lives in the field, not the hand.</summary>
    void MoveTo(double x, double y)
    {
        _flingX = x - _drag.X;
        _flingY = y - _drag.Y;

        _drag.X = x;
        _drag.Y = y;

        Moved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Momentum: the card keeps going after the hand stops, and coasts to rest.
    /// A fixed-duration slide would land on the same spot every time no matter
    /// how hard it was thrown, which is the tell of a fake drag.
    /// </summary>
    void StartCoast()
    {
        if (!Anim.Enabled || (Math.Abs(_flingX) < 0.6 && Math.Abs(_flingY) < 0.6))
        {
            _flingX = 0;
            _flingY = 0;
            return;
        }

        _coast = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _coast.Tick += OnCoast;
        _coast.Start();
    }

    void OnCoast(object? sender, object e)
    {
        _flingX *= CoastDamp;
        _flingY *= CoastDamp;

        var x = _drag.X + _flingX;
        var y = _drag.Y + _flingY;
        var wantX = x;
        var wantY = y;

        Clamp(ref x, ref y);

        // an edge absorbs the throw rather than bouncing it
        if (Math.Abs(x - wantX) > 0.001)
        {
            _flingX = 0;
        }

        if (Math.Abs(y - wantY) > 0.001)
        {
            _flingY = 0;
        }

        MoveTo(x, y);

        if (Math.Abs(_flingX) < 0.1 && Math.Abs(_flingY) < 0.1)
        {
            StopCoast();
        }
    }

    void StopCoast()
    {
        if (_coast is null)
        {
            return;
        }

        _coast.Stop();
        _coast.Tick -= OnCoast;
        _coast = null;
        _flingX = 0;
        _flingY = 0;
    }

    /// <summary>Keeps a grabbable edge of the card on screen at every offset.</summary>
    void Clamp(ref double x, ref double y)
    {
        if (XamlRoot is not { } root || Space is not { } space || ActualWidth <= 0)
        {
            return;
        }

        // where the card would sit with no drag applied
        var origin = TransformToVisual(space).TransformPoint(new Point(0, 0));
        var homeX = origin.X - _drag.X;
        var homeY = origin.Y - _drag.Y;

        x = Math.Clamp(x, EdgeKeep - ActualWidth - homeX, root.Size.Width - EdgeKeep - homeX);
        y = Math.Clamp(y, EdgeKeep - ActualHeight - homeY, root.Size.Height - EdgeKeep - homeY);
    }

    /// <summary>
    /// The card's own parent: the one element every drag measurement can be
    /// taken against safely. A RenderTransform on this card cannot move its
    /// parent, so the reference frame holds still while the card travels.
    /// Passing null instead reads as the sender's space on Uno, and the sender
    /// changes to whichever child is under the pointer, which throws the card
    /// across the page.
    /// </summary>
    UIElement? Space => VisualTreeHelper.GetParent(this) as UIElement;

    static bool StartedInside(object? source, DependencyObject subtree)
    {
        var node = source as DependencyObject;

        while (node is not null)
        {
            if (node == subtree)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}
