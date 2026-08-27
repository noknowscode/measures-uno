using Measures.Charts;
using Windows.Foundation;

namespace Measures.Tests;

public class CurvesTests
{
    static readonly Point[] Nodes =
    [
        new Point(0, 50),
        new Point(40, 10),
        new Point(100, 30),
    ];

    [Test]
    public void Midpoint_PutsBothControlsOnTheVerticalThroughTheMidpoint()
    {
        var segments = Curves.Midpoint(Nodes);

        segments.Should().HaveCount(2);

        var first = segments[0];
        first.C1.X.Should().BeApproximately(20, 0.001);
        first.C2.X.Should().BeApproximately(20, 0.001);
        first.C1.Y.Should().Be(first.P0.Y);
        first.C2.Y.Should().Be(first.P3.Y);
    }

    [Test]
    public void Midpoint_KeepsTheCurveInsideTheHullOfItsNodes()
    {
        var sampler = CubicSampler.FromSegments(Curves.Midpoint(Nodes));

        var lowest = sampler.Points.Min(p => p.Y);
        var highest = sampler.Points.Max(p => p.Y);

        lowest.Should().BeGreaterThanOrEqualTo(10 - 0.001);
        highest.Should().BeLessThanOrEqualTo(50 + 0.001);
    }

    [Test]
    public void FlatTangent_LeavesAndArrivesLevel()
    {
        var segments = Curves.FlatTangent(Nodes, tension: 0.4);

        foreach (var segment in segments)
        {
            segment.C1.Y.Should().Be(segment.P0.Y);
            segment.C2.Y.Should().Be(segment.P3.Y);
        }
    }

    [Test]
    public void FlatTangent_AppliesTheLeadOverridesOnlyAtTheEnds()
    {
        var segments = Curves.FlatTangent(Nodes, tension: 0.4, leadIn: 0.1, leadOut: 0.9);

        // first segment leaves with the lead-in tension
        segments[0].C1.X.Should().BeApproximately(0 + 40 * 0.1, 0.001);

        // last segment arrives with the lead-out tension
        segments[^1].C2.X.Should().BeApproximately(100 - 60 * 0.9, 0.001);

        // the shared boundary keeps the default tension on both sides
        segments[0].C2.X.Should().BeApproximately(40 - 40 * 0.4, 0.001);
        segments[^1].C1.X.Should().BeApproximately(40 + 60 * 0.4, 0.001);
    }

    [Test]
    public void Steps_MakeOnePlateauPerSlotAndAVerticalStepBetween()
    {
        var points = Curves.Steps([10, 50, 90, 130], [30, 0, 60]);

        points.Should().HaveCount(6);

        // a plateau is two points at the same level, slot edge to slot edge
        points[0].Should().Be(new Point(10, 30));
        points[1].Should().Be(new Point(50, 30));

        // the step is vertical: same x, new level
        points[2].X.Should().Be(points[1].X);
        points[2].Y.Should().Be(0);

        points[^1].Should().Be(new Point(130, 60));
    }
}
