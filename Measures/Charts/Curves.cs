using Microsoft.UI.Xaml.Media;

namespace Measures.Charts;

/// <summary>
/// The two smoothings the page uses, plus the bridge from segments to a
/// PathGeometry. Both smoothings keep the curve inside the hull of its nodes,
/// which is what stops the ridge from overshooting a quiet day into a dip.
/// </summary>
public static class Curves
{
    /// <summary>
    /// Midpoint smoothing: control points sit on the vertical through the
    /// midpoint of each pair. This is the ridge.
    /// </summary>
    public static List<CubicSegment> Midpoint(IReadOnlyList<Point> nodes)
    {
        var segments = new List<CubicSegment>(Math.Max(nodes.Count - 1, 0));

        for (var i = 1; i < nodes.Count; i++)
        {
            var a = nodes[i - 1];
            var b = nodes[i];
            var mx = (a.X + b.X) / 2;

            segments.Add(new CubicSegment(a, new Point(mx, a.Y), new Point(mx, b.Y), b));
        }

        return segments;
    }

    /// <summary>
    /// Flat-tangent smoothing: the curve leaves and arrives level, which is how
    /// a night settles into a trough and lifts back out of it.
    /// <paramref name="leadIn"/> and <paramref name="leadOut"/> override the
    /// tension on the first and last segments, so falling asleep and waking are
    /// steeper than the plateaus between.
    /// </summary>
    public static List<CubicSegment> FlatTangent(
        IReadOnlyList<Point> nodes,
        double tension = 0.4,
        double? leadIn = null,
        double? leadOut = null)
    {
        var segments = new List<CubicSegment>(Math.Max(nodes.Count - 1, 0));
        var last = nodes.Count - 2;

        for (var i = 1; i < nodes.Count; i++)
        {
            var a = nodes[i - 1];
            var b = nodes[i];
            var dx = b.X - a.X;

            var k1 = i - 1 == 0 ? leadIn ?? tension : tension;
            var k2 = i - 1 == last ? leadOut ?? tension : tension;

            segments.Add(new CubicSegment(
                a,
                new Point(a.X + dx * k1, a.Y),
                new Point(b.X - dx * k2, b.Y),
                b));
        }

        return segments;
    }

    public static PathGeometry ToGeometry(IReadOnlyList<CubicSegment> segments, bool closeToY = false, double closeY = 0)
    {
        var figure = new PathFigure { StartPoint = segments[0].P0, IsFilled = closeToY };

        foreach (var segment in segments)
        {
            figure.Segments.Add(new BezierSegment
            {
                Point1 = segment.C1,
                Point2 = segment.C2,
                Point3 = segment.P3,
            });
        }

        if (closeToY)
        {
            figure.Segments.Add(new LineSegment { Point = new Point(segments[^1].P3.X, closeY) });
            figure.Segments.Add(new LineSegment { Point = new Point(segments[0].P0.X, closeY) });
            figure.IsClosed = true;
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return geometry;
    }
}
