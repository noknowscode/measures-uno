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
/// </summary>
public sealed partial class MeasureCard : ContentControl
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

    FrameworkElement? _riseHost;
    TextBlock? _numeral;
    Grid? _axis;
    ICaptionSource? _source;
    bool _loaded;
    bool _risen;

    public MeasureCard()
    {
        DefaultStyleKey = typeof(MeasureCard);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

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

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _riseHost = GetTemplateChild("PART_Rise") as FrameworkElement;
        _numeral = GetTemplateChild("PART_Numeral") as TextBlock;
        _axis = GetTemplateChild("PART_Axis") as Grid;
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

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
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

    void BuildAxis()
    {
        if (_axis is null)
        {
            return;
        }

        _axis.Children.Clear();
        _axis.ColumnDefinitions.Clear();

        var labels = (AxisLabels ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < labels.Length; i++)
        {
            _axis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var text = new SpacedText
            {
                Text = labels[i],
                Style = (Style)Application.Current.Resources["AxisStyle"],
                HorizontalAlignment = i == 0
                    ? HorizontalAlignment.Left
                    : i == labels.Length - 1
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Center,
            };

            Grid.SetColumn(text, i);
            _axis.Children.Add(text);
        }
    }
}
