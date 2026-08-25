using Measures.Charts;
using Windows.Foundation;

namespace Measures.Tests;

public class CubicSamplerTests
{
    /// <summary>A cubic whose control points sit on the line is a straight line.</summary>
    static CubicSegment Straight(Point from, Point to) =>
        new(from,
            new Point(from.X + (to.X - from.X) / 3, from.Y + (to.Y - from.Y) / 3),
            new Point(from.X + (to.X - from.X) * 2 / 3, from.Y + (to.Y - from.Y) * 2 / 3),
            to);

    [Test]
    public void TotalLength_OfAStraightSegment_IsTheDistance()
    {
        var sampler = CubicSampler.FromSegments([Straight(new Point(0, 0), new Point(30, 40))]);

        sampler.TotalLength.Should().BeApproximately(50, 0.001);
    }

    [Test]
    public void TotalLength_AccumulatesAcrossSegments()
    {
        var sampler = CubicSampler.FromSegments(
        [
            Straight(new Point(0, 0), new Point(10, 0)),
            Straight(new Point(10, 0), new Point(10, 10)),
        ]);

        sampler.TotalLength.Should().BeApproximately(20, 0.001);
    }

    [Test]
    public void PointAtLength_HalfWay_IsTheMidpoint()
    {
        var sampler = CubicSampler.FromSegments([Straight(new Point(0, 0), new Point(100, 0))]);

        var point = sampler.PointAtLength(sampler.TotalLength / 2);

        point.X.Should().BeApproximately(50, 0.01);
        point.Y.Should().BeApproximately(0, 0.01);
    }

    [Test]
    public void PointAtLength_ClampsBeyondBothEnds()
    {
        var sampler = CubicSampler.FromSegments([Straight(new Point(2, 3), new Point(8, 9))]);

        sampler.PointAtLength(-10).Should().Be(sampler.Start);
        sampler.PointAtLength(1000).Should().Be(sampler.End);
    }

    [Test]
    public void PointAtX_InterpolatesBetweenSamples()
    {
        // a symmetric arc: the apex sits at the midpoint in x
        var sampler = CubicSampler.FromSegments(
        [
            new CubicSegment(new Point(0, 100), new Point(50, 0), new Point(150, 0), new Point(200, 100)),
        ]);

        var apex = sampler.PointAtX(100);

        apex.X.Should().BeApproximately(100, 0.5);
        apex.Y.Should().BeLessThan(100);

        // and it is symmetric about that apex
        sampler.PointAtX(60).Y.Should().BeApproximately(sampler.PointAtX(140).Y, 0.5);
    }

    [Test]
    public void PointAtX_ClampsOutsideTheDomain()
    {
        var sampler = CubicSampler.FromSegments([Straight(new Point(10, 5), new Point(90, 5))]);

        sampler.PointAtX(0).X.Should().BeApproximately(10, 0.001);
        sampler.PointAtX(500).X.Should().BeApproximately(90, 0.001);
    }

    [Test]
    public void Evaluate_AtTheEnds_ReturnsTheAnchors()
    {
        var segment = new CubicSegment(new Point(1, 2), new Point(3, 9), new Point(7, 9), new Point(9, 2));

        CubicSampler.Evaluate(segment, 0).Should().Be(new Point(1, 2));
        CubicSampler.Evaluate(segment, 1).Should().Be(new Point(9, 2));
    }

    [Test]
    public void FromSegments_RejectsAnEmptyRun()
    {
        var act = () => CubicSampler.FromSegments([]);

        act.Should().Throw<ArgumentException>();
    }
}
