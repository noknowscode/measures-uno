using Measures.Motion;
using Measures.Theming;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Measures.Controls;

/// <summary>
/// The temperature of light a card is lit by, named for what it lights: one
/// per instrument - four rooms lit differently, and the only place colour is
/// allowed on the page. Hue tells the rooms apart and never the state.
/// </summary>
public enum Temperature
{
    /// <summary>Butter: dawn on the ridge.</summary>
    Effort,

    /// <summary>Sage: air after rain.</summary>
    Frequency,

    /// <summary>Lavender: dusk over the valley.</summary>
    Rest,

    /// <summary>Blush: late sun on the road.</summary>
    Distance,
}

/// <summary>
/// The device face every measure sits in, with one rigid anatomy: label and
/// period, value and unit, chart, rule, context. It owns all the chrome, so
/// the four charts only ever draw their plot; numeral tops, chart boxes and
/// axis rows are shared horizontals across the bench, and they line up because
/// this control decides them, not the charts. The metric column is anchored to
/// the top with a fixed gap to the chart, so slack never pools between them.
///
/// It also owns the room's light: the chart names a <see cref="Temperature"/>
/// and the card renders it as a wash descending from its top edge. The chart
/// never paints its own edge light.
/// </summary>
public sealed partial class MeasureCard : ContentControl
{
    public static readonly DependencyProperty HeadLeftProperty = DependencyProperty.Register(
        nameof(HeadLeft), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeadRightProperty = DependencyProperty.Register(
        nameof(HeadRight), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// True when the period label is a live range switcher. A caret is drawn
    /// only where a tap does something: an affordance that does nothing is a lie.
    /// </summary>
    public static readonly DependencyProperty IsRangeSwitchProperty = DependencyProperty.Register(
        nameof(IsRangeSwitch), typeof(bool), typeof(MeasureCard), new PropertyMetadata(false, OnRangeSwitchChanged));

    public static readonly DependencyProperty FootRightProperty = DependencyProperty.Register(
        nameof(FootRight), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NumeralValueProperty = DependencyProperty.Register(
        nameof(NumeralValue), typeof(string), typeof(MeasureCard), new PropertyMetadata("—", OnNumeralChanged));

    public static readonly DependencyProperty NumeralSuffixProperty = DependencyProperty.Register(
        nameof(NumeralSuffix), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty, OnNumeralChanged));

    public static readonly DependencyProperty NumeralTailProperty = DependencyProperty.Register(
        nameof(NumeralTail), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty, OnNumeralChanged));

    /// <summary>Comma separated axis whispers, laid out under the chart's data span.</summary>
    public static readonly DependencyProperty AxisLabelsProperty = DependencyProperty.Register(
        nameof(AxisLabels), typeof(string), typeof(MeasureCard), new PropertyMetadata(string.Empty, OnAxisChanged));

    public static readonly DependencyProperty RiseDelayProperty = DependencyProperty.Register(
        nameof(RiseDelay), typeof(double), typeof(MeasureCard), new PropertyMetadata(0d));

    /// <summary>The temperature of light this room is lit by.</summary>
    public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(
        nameof(Temperature), typeof(Temperature), typeof(MeasureCard),
        new PropertyMetadata(Temperature.Effort, OnTemperatureChanged));

    /// <summary>
    /// How far the axis whispers are pulled in from the plot's edges, as a
    /// fraction of its width. Every chart's data spans x 10 to 290 of its 300
    /// box, so the whispers share that 3.3% and sit under the first and last
    /// data point rather than under the box.
    /// </summary>
    public static readonly DependencyProperty AxisInsetProperty = DependencyProperty.Register(
        nameof(AxisInset), typeof(double), typeof(MeasureCard), new PropertyMetadata(10 / 300d, OnAxisChanged));

    readonly TranslateTransform _rise = new();

    /// <summary>
    /// The edge light, built here rather than in the dictionary: every card is
    /// a different temperature, so the brush cannot be a shared resource.
    /// </summary>
    readonly LinearGradientBrush _edgeLight = new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops =
        {
            new GradientStop { Offset = 0 },
            new GradientStop { Offset = 0.45 },
            new GradientStop { Offset = 1 },
        },
    };

    FrameworkElement? _riseHost;
    TextBlock? _numeral;
    Canvas? _axis;
    FrameworkElement? _headLabel;
    RangeSwitch? _range;
    ICaptionSource? _source;
    bool _loaded;
    bool _risen;

    public MeasureCard()
    {
        DefaultStyleKey = typeof(MeasureCard);

        LightTheRoom();
        Palette.Recoloured += OnRecoloured;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Raised when the range switcher is tapped. The page decides what the next range is.</summary>
    public event EventHandler? RangeToggled;

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

    public bool IsRangeSwitch
    {
        get => (bool)GetValue(IsRangeSwitchProperty);
        set => SetValue(IsRangeSwitchProperty, value);
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

    public Temperature Temperature
    {
        get => (Temperature)GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }

    public double AxisInset
    {
        get => (double)GetValue(AxisInsetProperty);
        set => SetValue(AxisInsetProperty, value);
    }

    public CaptionPresenter? Caption { get; private set; }

    /// <summary>Line box for the axis row, at the 8.5 epx whisper size.</summary>
    const double AxisHeight = 12;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _riseHost = GetTemplateChild("PART_Rise") as FrameworkElement;

        // the template supplies the layer; the card supplies the light, because
        // each room is a different temperature and so cannot share a brush
        if (GetTemplateChild("PART_EdgeLight") is Shape edge)
        {
            edge.Fill = _edgeLight;
        }

        _numeral = GetTemplateChild("PART_Numeral") as TextBlock;
        _axis = GetTemplateChild("PART_Axis") as Canvas;
        _headLabel = GetTemplateChild("PART_HeadLabel") as FrameworkElement;

        if (_range is not null)
        {
            _range.Click -= OnRangeClick;
        }

        _range = GetTemplateChild("PART_Range") as RangeSwitch;

        if (_range is not null)
        {
            _range.Click += OnRangeClick;
        }

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

        ShowRange();
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

    static void OnTemperatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MeasureCard)d).LightTheRoom();

    static void OnRangeSwitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MeasureCard)d).ShowRange();

    static Palette.Token TokenFor(Temperature temperature) => temperature switch
    {
        Temperature.Frequency => Palette.Token.Sage,
        Temperature.Rest => Palette.Token.Lavender,
        Temperature.Distance => Palette.Token.Blush,
        _ => Palette.Token.Butter,
    };

    /// <summary>
    /// The wash descending from the top edge. Never animated: a temperature is
    /// a room, not an effect, and a wash that moves has become a blob.
    /// </summary>
    void LightTheRoom()
    {
        var token = TokenFor(Temperature);
        var edge = Palette.Intensity(Palette.Wash.Edge);

        _edgeLight.GradientStops[0].Color = Palette.Get(token, edge);
        _edgeLight.GradientStops[1].Color = Palette.Get(token, edge * 0.25);
        _edgeLight.GradientStops[2].Color = Palette.Get(token, 0d);
    }

    void OnRecoloured(object? sender, EventArgs e) => LightTheRoom();

    /// <summary>The head shows either the plain period or the switcher, never both.</summary>
    void ShowRange()
    {
        if (_headLabel is not null)
        {
            _headLabel.Visibility = IsRangeSwitch ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_range is not null)
        {
            _range.Visibility = IsRangeSwitch ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    void OnRangeClick(object sender, RoutedEventArgs e) => RangeToggled?.Invoke(this, EventArgs.Empty);

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

        Anim.Animate(_riseHost, "Opacity", 0, 1, 900, Anim.EaseHands, RiseDelay);
        Anim.Animate(_rise, "Y", 14, 0, 900, Anim.EaseHands, RiseDelay);
    }

    /// <summary>
    /// Asks the instrument to draw itself in again. The card itself does not
    /// rise a second time: the rise is an arrival, and the cards have already
    /// arrived - replaying it would just make the page flinch.
    /// </summary>
    public void Replay() => _source?.Replay();

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Palette.Recoloured -= OnRecoloured;

        if (_source is not null)
        {
            _source.ReadingChanged -= OnReadingChanged;
            _source = null;
        }
    }

    /// <summary>
    /// The numeral is value and unit, and sometimes a trailing figure ("7 h 32").
    /// The unit is 41% of the value, a step back in colour, and a hair of
    /// tracking away from it. Built in code because named Runs inside a
    /// template are not reliably reachable.
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
                Foreground = (Brush)Application.Current.Resources["UnitBrush"],
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

        // the run the whispers describe is the data, not the box: pulling the
        // ends in by the inset puts them under the first and last point
        var inset = Math.Clamp(AxisInset, 0, 0.25) * width;
        var run = width - 2 * inset;

        for (var i = 0; i <= last; i++)
        {
            var label = (FrameworkElement)_axis.Children[i];

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // the ends sit flush to the run, everything between is centred on its tick
            var fraction = last == 0 ? 0 : (double)i / last;

            Canvas.SetLeft(label, inset + fraction * run - label.DesiredSize.Width * fraction);
        }
    }
}
