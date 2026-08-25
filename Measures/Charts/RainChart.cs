using Measures.Controls;
using Measures.Data;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Measures.Charts;

/// <summary>
/// A fortnight of focus as rainfall. The gesture is <em>droplet</em>: tap a day
/// and its drop detaches and falls the stem's own height, which is what turns a
/// bar chart into weather.
/// </summary>
public sealed partial class RainChart : ChartBase
{
    /// <summary>Flex gap between stems, as a fraction of the row width.</summary>
    const double GapFraction = 0.045;

    const double MinStemFraction = 0.03;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(SessionLog), typeof(RainChart), new PropertyMetadata(null, OnDataChanged));

    readonly List<StemButton> _stems = [];

    Grid? _row;
    int? _selected;

    public RainChart()
    {
        DefaultStyleKey = typeof(RainChart);
        PlotHeight = 92;
    }

    public SessionLog? ChartData
    {
        get => (SessionLog?)GetValue(ChartDataProperty);
        set => SetValue(ChartDataProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        _row = GetTemplateChild("PART_Row") as Grid;

        base.OnApplyTemplate();
    }

    static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (RainChart)d;

        if (chart.PlotWidth > 0)
        {
            chart.Rebuild(chart.PlotWidth, chart.PlotHeight);
            chart.PublishDefault();
        }
    }

    protected override void Rebuild(double width, double height)
    {
        if (_row is null)
        {
            return;
        }

        ClearStems();

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            return;
        }

        var days = data.Days;
        var ceiling = Math.Max(data.Max, 1);

        _row.ColumnSpacing = width * GapFraction;
        _row.Height = height;

        for (var i = 0; i < days.Count; i++)
        {
            _row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var stem = new StemButton
            {
                StemHeight = Math.Max((double)days[i].Count / ceiling * height, height * MinStemFraction),
                Tag = i,
            };

            AutomationProperties.SetName(
                stem,
                $"{Format.DayLabel(days[i].Date)}, {days[i].Count} {(days[i].Count == 1 ? "session" : "sessions")}");

            stem.Click += OnStemClick;
            stem.PointerEntered += OnStemEntered;
            stem.PointerExited += OnStemExited;

            Grid.SetColumn(stem, i);
            _row.Children.Add(stem);
            _stems.Add(stem);
        }

        Render();
    }

    protected override void PlayEntrance()
    {
        // the fortnight falls left to right, one day behind the last
        for (var i = 0; i < _stems.Count; i++)
        {
            _stems[i].PlayEntrance(i * 50);
        }
    }

    protected override void PublishDefault()
    {
        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            Publish(Reading.Default("no sessions logged"));
            return;
        }

        Publish(Reading.Default(Invited
            ? $"{data.PerDay:0.0} a day — a steady fortnight"
            : "tap a stem to read its day"));
    }

    void ClearStems()
    {
        foreach (var stem in _stems)
        {
            stem.Click -= OnStemClick;
            stem.PointerEntered -= OnStemEntered;
            stem.PointerExited -= OnStemExited;
            stem.SetBead(false);
        }

        _stems.Clear();
        _row?.Children.Clear();
        _row?.ColumnDefinitions.Clear();
    }

    void OnStemClick(object sender, RoutedEventArgs e)
    {
        var index = (int)((StemButton)sender).Tag;
        var days = ChartData!.Days;

        MarkInvited();

        var reselected = _selected != index;
        _selected = reselected ? index : null;

        Render();

        if (reselected && days[index].Count > 0)
        {
            _stems[index].Shed();
        }
    }

    void OnStemEntered(object sender, PointerRoutedEventArgs e) =>
        Publish(Reading.Preview(ReadingFor((int)((StemButton)sender).Tag)));

    void OnStemExited(object sender, PointerRoutedEventArgs e) => PublishCurrent();

    string ReadingFor(int index)
    {
        var day = ChartData!.Days[index];

        return $"{Format.DayLabel(day.Date)} — {day.Count} {(day.Count == 1 ? "session" : "sessions")}";
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

    void Render()
    {
        var today = _stems.Count - 1;

        for (var i = 0; i < _stems.Count; i++)
        {
            // with nothing chosen, today is the day being read
            _stems[i].IsOn = _selected == i || (_selected is null && i == today);
            _stems[i].SetBead(i == today);
        }

        PublishCurrent();
    }
}
