using Measures.Controls;
using Measures.Motion;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Measures.Charts;

/// <summary>
/// What every instrument on the bench shares: a plot canvas that recomputes its
/// geometry whenever the card changes width, one entrance that plays once, and
/// a line to say. Viewbox is deliberately not used - it would scale the strokes
/// with the chart, and a hairline has to stay a hairline at any card width.
/// </summary>
public abstract partial class ChartBase : Control, ICaptionSource
{
    public static readonly DependencyProperty PlotHeightProperty = DependencyProperty.Register(
        nameof(PlotHeight), typeof(double), typeof(ChartBase), new PropertyMetadata(100d));

    double _lastWidth;
    bool _entrancePlayed;

    protected ChartBase()
    {
        SizeChanged += OnSizeChanged;
    }

    public event EventHandler<Reading>? ReadingChanged;

    /// <summary>The chart's design height. Vertical proportions never scale.</summary>
    public double PlotHeight
    {
        get => (double)GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    protected Canvas? Plot { get; private set; }

    /// <summary>True once the reader has performed this chart's gesture.</summary>
    protected bool Invited { get; private set; }

    protected double PlotWidth => _lastWidth;

    public void PublishInitialReading() => PublishDefault();

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Plot = GetTemplateChild("PART_Plot") as Canvas;

        if (ActualWidth > 0)
        {
            Layout(ActualWidth);
        }
    }

    /// <summary>Lays the chart out at a given width. Called on every resize.</summary>
    protected abstract void Rebuild(double width, double height);

    /// <summary>The entrance is the loading state; it plays once, after first layout.</summary>
    protected abstract void PlayEntrance();

    /// <summary>The line the card shows when nothing is selected.</summary>
    protected abstract void PublishDefault();

    protected void Publish(Reading reading) => ReadingChanged?.Invoke(this, reading);

    protected void MarkInvited() => Invited = true;

    /// <summary>
    /// Reveals a stroked path from its start. XAML has no pathLength
    /// normalisation, so the dash is expressed in stroke-thickness units off
    /// the measured polyline length.
    /// </summary>
    protected static void DrawIn(Path path, double length, double ms, double delayMs, KeySpline spline)
    {
        var units = length / Math.Max(path.StrokeThickness, 0.0001);

        path.StrokeDashArray = [units, units];
        path.StrokeDashOffset = units;

        Anim.Animate(path, "StrokeDashOffset", units, 0, ms, spline, delayMs);
    }

    /// <summary>The late fade the mist and the sky share: hold, then arrive.</summary>
    protected static void FadeLate(UIElement element, double totalMs = 2400)
    {
        element.Opacity = 0;
        Anim.Animate(element, "Opacity", 0, 1, totalMs * 0.6, Anim.Ease, totalMs * 0.4);
    }

    protected static void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
    }

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

        Rebuild(width, PlotHeight);

        if (_entrancePlayed)
        {
            return;
        }

        _entrancePlayed = true;
        PlayEntrance();
    }
}
