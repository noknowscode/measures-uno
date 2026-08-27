using Measures.Data;

namespace Measures.Tests;

public class ModelTests
{
    [Test]
    public void Ridge_DerivesTheNumeralTheCaptionAndThePeak()
    {
        var ridge = SampleData.Ridge;

        ridge.Days.Should().HaveCount(7);
        ridge.Total.Should().BeApproximately(24.6, 0.0001);
        ridge.Average.Should().BeApproximately(24.6 / 7, 0.0001);
        ridge.Peak.Date.DayOfWeek.Should().Be(DayOfWeek.Saturday);
        ridge.PeakIndex.Should().Be(5);
        ridge.Max.Should().BeApproximately(5.2, 0.0001);
    }

    [Test]
    public void Ridge_ReadsMondayToSundayWhateverTodayIs()
    {
        var days = SampleData.Ridge.Days;

        days[0].Date.DayOfWeek.Should().Be(DayOfWeek.Monday);
        days[^1].Date.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        days[^1].Date.Should().BeOnOrBefore(DateOnly.FromDateTime(DateTime.Now));
    }

    [Test]
    public void Ridge_TheFortnightWindowKeepsTheWeekAsItsTail()
    {
        var fortnight = SampleData.RidgeHistory.Last(14);

        fortnight.Days.Should().HaveCount(14);
        fortnight.Days.TakeLast(7).Should().Equal(SampleData.Ridge.Days);
    }

    [Test]
    public void Sessions_CountAndDailyAverageMatchTheCard()
    {
        var sessions = SampleData.Sessions;

        sessions.Days.Should().HaveCount(14);
        sessions.Total.Should().Be(48);
        sessions.PerDay.Should().BeApproximately(48 / 14d, 0.0001);
        sessions.Days[^1].Date.Should().Be(DateOnly.FromDateTime(DateTime.Now));
        sessions.PeakIndex.Should().Be(5);
    }

    [Test]
    public void Sessions_ThePriorPeriodIsTheSameLengthAndEndsWhereTheWindowStarts()
    {
        var history = SampleData.SessionsHistory;
        var week = history.Last(7);
        var prior = history.Before(7);

        prior.Days.Should().HaveCount(7);
        prior.Days[^1].Date.AddDays(1).Should().Be(week.Days[0].Date);
        week.Total.Should().Be(25);
        prior.Total.Should().Be(23);

        history.Before(14).Days.Should().HaveCount(14);
        history.Before(14).Total.Should().Be(39);
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
        new RidgeData([new DayValue(new DateOnly(2026, 8, 24), 3)]).IsEmpty.Should().BeTrue();
        SampleData.Ridge.IsEmpty.Should().BeFalse();
    }

    [Test]
    public void Format_SpeaksInSignedDeltasWithATrueMinus()
    {
        Format.DeltaPercent(5.2, 24.6 / 7).Should().Be("+48 %");
        Format.DeltaPercent(1.2, 24.6 / 7).Should().Be("−66 %");
        Format.DeltaPercent(3, 0).Should().Be("—");
        Format.DeltaCount(3).Should().Be("+3");
        Format.DeltaCount(-2).Should().Be("−2");
        Format.DeltaCount(0).Should().Be("±0");
        Format.DeltaHours(-0.35).Should().Be("−0.4 h");
    }
}
