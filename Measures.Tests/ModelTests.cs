using Measures.Data;

namespace Measures.Tests;

public class ModelTests
{
    [Test]
    public void Ridge_DerivesTheNumeralTheCaptionAndThePeak()
    {
        var ridge = SampleData.Ridge;

        ridge.Total.Should().BeApproximately(24.6, 0.0001);
        ridge.Average.Should().BeApproximately(24.6 / 7, 0.0001);
        ridge.Peak.Full.Should().Be("saturday");
        ridge.Max.Should().BeApproximately(5.2, 0.0001);
    }

    [Test]
    public void Sessions_CountAndDailyAverageMatchTheCard()
    {
        var sessions = SampleData.Sessions;

        sessions.Days.Should().HaveCount(14);
        sessions.Total.Should().Be(48);
        sessions.PerDay.Should().BeApproximately(48 / 14d, 0.0001);
        sessions.Days[^1].Date.Should().Be(DateOnly.FromDateTime(DateTime.Now));
    }

    [Test]
    public void Night_SleptSpansMidnight()
    {
        var night = SampleData.Night;

        night.Slept.Should().Be(new TimeSpan(7, 32, 0));
    }

    [Test]
    public void Night_FractionAndAtAreInverses()
    {
        var night = SampleData.Night;

        night.Fraction(new TimeOnly(21, 0)).Should().BeApproximately(0, 0.0001);
        night.Fraction(new TimeOnly(9, 0)).Should().BeApproximately(1, 0.0001);
        night.Fraction(new TimeOnly(3, 0)).Should().BeApproximately(0.5, 0.0001);

        night.At(0).Should().Be(new TimeOnly(21, 0));
        night.At(0.5).Should().Be(new TimeOnly(3, 0));
        night.At(night.Fraction(night.Down)).Should().Be(night.Down);
        night.At(night.Fraction(night.Up)).Should().Be(night.Up);
    }

    [Test]
    public void Runs_ProgressIsTheRoadTheRunnerHasCovered()
    {
        var runs = SampleData.Runs;

        runs.Ran.Should().BeApproximately(18.2, 0.0001);
        runs.Left.Should().BeApproximately(6.8, 0.0001);
        runs.Progress.Should().BeApproximately(0.728, 0.0001);
    }

    [Test]
    public void Runs_WithNoGoal_ReadAsEmptyRatherThanDividingByZero()
    {
        var runs = new RunLog([new Run("mon", 5)], GoalKm: 0, PaceMinPerKm: 5.8);

        runs.IsEmpty.Should().BeTrue();
        runs.Progress.Should().Be(0);
    }

    [Test]
    public void Ridge_WithASingleDay_ReadsAsEmptyBecauseItCannotBeACurve()
    {
        new RidgeData([new DayValue("mon", "monday", 3)]).IsEmpty.Should().BeTrue();
        SampleData.Ridge.IsEmpty.Should().BeFalse();
    }
}
