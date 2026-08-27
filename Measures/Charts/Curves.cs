using Microsoft.UI.Xaml.Media;

namespace Measures.Charts;

/// <summary>
/// The smoothings the page uses, the one shape that is deliberately not smooth,
/// plus the bridge from segments to a PathGeometry. Both smoothings keep the
/// curve inside the hull of its nodes, which is what stops the ridge from
/// overshooting a quiet day into a dip.
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

    /// <summary>
    /// A staircase: one flat plateau per slot at its level, a sharp vertical
    /// step between neighbours. Integer data gets integer geometry - this is
    /// the sessions line, angular where the ridge is smooth.
    /// <paramref name="edges"/> has one more entry than <paramref name="levels"/>.
    /// </summary>
    public static List<Point> Steps(IReadOnlyList<double> edges, IReadOnlyList<double> levels)
    {
        var points = new List<Point>(levels.Count * 2);

        for (var i = 0; i < levels.Count; i++)
        {
            points.Add(new Point(edges[i], levels[i]));
            points.Add(new Point(edges[i + 1], levels[i]));
        }

        return points;
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

    /// <summary>A polyline through the points, optionally closed down to a baseline.</summary>
    public static PathGeometry ToGeometry(IReadOnlyList<Point> points, bool closeToY = false, double closeY = 0)
    {
        var figure = new PathFigure { StartPoint = points[0], IsFilled = closeToY };

        for (var i = 1; i < points.Count; i++)
        {
            figure.Segments.Add(new LineSegment { Point = points[i] });
        }

        if (closeToY)
        {
            figure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, closeY) });
            figure.Segments.Add(new LineSegment { Point = new Point(points[0].X, closeY) });
            figure.IsClosed = true;
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return geometry;
    }

    /// <summary>
    /// A horizontal reference rule as a run of real dashes. StrokeDashArray does
    /// not render on this target, so the gaps are gaps in the geometry.
    /// </summary>
    public static PathGeometry DashedRule(double x1, double x2, double y, double dash, double gap)
    {
        var geometry = new PathGeometry();

        for (var at = x1; at < x2; at += dash + gap)
        {
            var figure = new PathFigure { StartPoint = new Point(at, y), IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = new Point(Math.Min(at + dash, x2), y) });
            geometry.Figures.Add(figure);
        }

        return geometry;
    }
}
