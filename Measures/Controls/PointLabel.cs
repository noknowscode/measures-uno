using Measures.Motion;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Measures.Controls;

/// <summary>
/// The two-line reading at a point: the date over the value, no tooltip chrome.
/// It sits above its point, clamped to the plot's edges, and flips below the
/// point when the top edge is too close. Fades in and out over the caption's
/// own 140 ms, so the chart and the foot change together.
/// </summary>
public sealed partial class PointLabel : StackPanel
{
    /// <summary>Clear of the mark, so the label never touches the dot it reads.</summary>
    const double Lift = 9;

    const double FadeMs = 140;

    readonly HaloText _date;
    readonly HaloText _value;

    public PointLabel()
    {
        var res = Application.Current.Resources;

        _date = new HaloText((double)res["ChartLabelSize"], (Brush)res["InkSoft45Brush"]);
        // the value keeps its case: "5.2 h", "1 session"
        _value = new HaloText((double)res["PointValueSize"], (Brush)res["InkBrush"], upperCase: false);

        Spacing = 1;
        Opacity = 0;
        IsHitTestVisible = false;

        Children.Add(_date);
        Children.Add(_value);
    }

    /// <summary>Reads at a point in the plot's coordinates, inside a plot of the given size.</summary>
    public void Show(Point at, string date, string value, double plotWidth, double plotHeight)
    {
        _date.Text = date;
        _value.Text = value;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = DesiredSize.Width;
        var height = DesiredSize.Height;

        var left = Math.Clamp(at.X - width / 2, 0, Math.Max(plotWidth - width, 0));
        var top = at.Y - Lift - height;

        // near the top edge the label would leave the plot, so it reads from below
        if (top < 0)
        {
            top = Math.Min(at.Y + Lift, Math.Max(plotHeight - height, 0));
        }

        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);

        if (Opacity < 1)
        {
            Anim.Animate(this, "Opacity", null, 1, FadeMs, Anim.EaseHands);
        }
    }

    public void Hide()
    {
        if (Opacity > 0)
        {
            Anim.Animate(this, "Opacity", null, 0, FadeMs, Anim.EaseHands);
        }
    }
}
