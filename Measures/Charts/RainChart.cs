using Measures.Controls;
using Measures.Data;
using Microsoft.UI.Xaml.Automation;

namespace Measures.Charts;

/// <summary>
/// A fortnight of focus as beads in a pool, lit sage - air after rain. A session
/// is a discrete countable thing, so the count is the chart: one bead per
/// session, readable without ever consulting the axis. The gesture is
/// <em>plip</em> - tap a day and its top bead hops and lands, a drop disturbing
/// the pool.
/// </summary>
public sealed partial class RainChart : ChartBase
{
    /// <summary>Flex gap between columns, as a fraction of the row width.</summary>
    const double GapFraction = 0.045;

    /// <summary>Each day rains in behind the last, left to right.</summary>
    const double DayStaggerMs = 50;

    public static readonly DependencyProperty ChartDataProperty = DependencyProperty.Register(
        nameof(ChartData), typeof(SessionLog), typeof(RainChart), new PropertyMetadata(null, OnDataChanged));

    readonly List<BeadColumn> _columns = [];

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
        if (Plot is null)
        {
            return;
        }

        ClearColumns();

        var data = ChartData;

        if (data is null || data.IsEmpty)
        {
            return;
        }

        var days = data.Days;

        // the flex row the prototype uses, resolved by hand
        var gap = width * GapFraction;
        var cell = Math.Max((width - gap * (days.Count - 1)) / days.Count, 1);

        for (var i = 0; i < days.Count; i++)
        {
            var column = new BeadColumn
            {
                Width = cell,
                Height = height,
                Tag = i,
            };

            // the day is the focus stop; individual beads are not reachable
            AutomationProperties.SetName(
                column,
                $"{Format.DayLabel(days[i].Date)}, {days[i].Count} {(days[i].Count == 1 ? "session" : "sessions")}");

            column.Click += OnColumnClick;
            column.PointerEntered += OnColumnEntered;
            column.PointerExited += OnColumnExited;

            Plot.Children.Add(column);
            Place(column, i * (cell + gap), 0);

            // the count is set once the column is in the tree, so its template
            // has been applied and the beads have somewhere to go
            column.Count = days[i].Count;
            _columns.Add(column);
        }

        Render();
    }

    protected override void PlayEntrance()
    {
        // the metric assembles in front of the reader: every bead rains in and
        // pools, left to right across the fortnight, bottom bead first per day
        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].PlayEntrance(i * DayStaggerMs);
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
            : "tap a day to read it"));
    }

    void ClearColumns()
    {
        foreach (var column in _columns)
        {
            column.Click -= OnColumnClick;
            column.PointerEntered -= OnColumnEntered;
            column.PointerExited -= OnColumnExited;
            column.SetBreathingTop(false);
            Plot?.Children.Remove(column);
        }

        _columns.Clear();
    }

    void OnColumnClick(object sender, RoutedEventArgs e)
    {
        var index = (int)((BeadColumn)sender).Tag;
        var days = ChartData!.Days;

        MarkInvited();

        var reselected = _selected != index;
        _selected = reselected ? index : null;

        Render();

        // Enter and Space arrive here too, so the keyboard gets the same
        // physical confirmation the pointer does
        if (reselected && days[index].Count > 0)
        {
            _columns[index].Plip();
        }
    }

    void OnColumnEntered(object sender, PointerRoutedEventArgs e) =>
        Publish(Reading.Preview(ReadingFor((int)((BeadColumn)sender).Tag)));

    void OnColumnExited(object sender, PointerRoutedEventArgs e) => PublishCurrent();

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
        var today = _columns.Count - 1;

        for (var i = 0; i < _columns.Count; i++)
        {
            // with nothing chosen, today is the day being read
            _columns[i].IsOn = _selected == i || (_selected is null && i == today);

            // the living edge lives in the data now: today's top bead breathes,
            // but only while nothing else is being read
            _columns[i].SetBreathingTop(i == today && _selected is null);
        }

        PublishCurrent();
    }
}
