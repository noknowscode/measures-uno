namespace Measures.Charts;

/// <summary>One cubic Bezier segment, in chart space.</summary>
public readonly record struct CubicSegment(Point P0, Point C1, Point C2, Point P3);

/// <summary>
/// Flattens a run of cubic segments into a polyline once, then answers
/// point-at-length and point-at-x from it. XAML has no pathLength
/// normalisation and no GetPointAtLength, so the draw-in dash maths, the night
/// scrub and the runner traversal all read their positions from here.
/// </summary>
public sealed class CubicSampler
{
    readonly Point[] _points;
    readonly double[] _cumulative;

    CubicSampler(Point[] points, double[] cumulative)
    {
        _points = points;
        _cumulative = cumulative;
        TotalLength = cumulative[^1];
    }

    public double TotalLength { get; }

    public IReadOnlyList<Point> Points => _points;

    public Point Start => _points[0];

    public Point End => _points[^1];

    public static CubicSampler FromSegments(IReadOnlyList<CubicSegment> segments, int perSegment = 48)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (segments.Count == 0)
        {
            throw new ArgumentException("At least one segment is required.", nameof(segments));
        }

        if (perSegment < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(perSegment));
        }

        var points = new Point[segments.Count * perSegment + 1];
        points[0] = segments[0].P0;

        var index = 1;

        foreach (var segment in segments)
        {
            for (var step = 1; step <= perSegment; step++)
            {
                points[index++] = Evaluate(segment, (double)step / perSegment);
            }
        }

        var cumulative = new double[points.Length];

        for (var i = 1; i < points.Length; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            cumulative[i] = cumulative[i - 1] + Math.Sqrt(dx * dx + dy * dy);
        }

        return new CubicSampler(points, cumulative);
    }

    public static Point Evaluate(CubicSegment s, double t)
    {
        var u = 1 - t;
        var uu = u * u;
        var tt = t * t;

        var a = uu * u;
        var b = 3 * uu * t;
        var c = 3 * u * tt;
        var d = tt * t;

        return new Point(
            a * s.P0.X + b * s.C1.X + c * s.C2.X + d * s.P3.X,
            a * s.P0.Y + b * s.C1.Y + c * s.C2.Y + d * s.P3.Y);
    }

    public Point PointAtLength(double length)
    {
        if (length <= 0)
        {
            return _points[0];
        }

        if (length >= TotalLength)
        {
            return _points[^1];
        }

        var hi = Array.BinarySearch(_cumulative, length);

        if (hi < 0)
        {
            hi = ~hi;
        }

        if (hi == 0)
        {
            return _points[0];
        }

        var span = _cumulative[hi] - _cumulative[hi - 1];
        var t = span <= 0 ? 0 : (length - _cumulative[hi - 1]) / span;

        return Lerp(_points[hi - 1], _points[hi], t);
    }

    /// <summary>
    /// Interpolated point at a given x. The curves this app draws are all
    /// monotonic in x, so a scan for the bracketing pair is exact.
    /// </summary>
    public Point PointAtX(double x)
    {
        if (x <= _points[0].X)
        {
            return _points[0];
        }

        if (x >= _points[^1].X)
        {
            return _points[^1];
        }

        for (var i = 1; i < _points.Length; i++)
        {
            if (_points[i].X < x)
            {
                continue;
            }

            var span = _points[i].X - _points[i - 1].X;
            var t = span <= 0 ? 0 : (x - _points[i - 1].X) / span;

            return Lerp(_points[i - 1], _points[i], t);
        }

        return _points[^1];
    }

    static Point Lerp(Point a, Point b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
