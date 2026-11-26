using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class PersonalAdaptationEngineTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly First = new(2026, 1, 1);
    private static readonly DateOnly Split = First.AddDays(12);
    private static readonly DateOnly Until = First.AddDays(20);
    private static readonly DateTimeOffset Now = At(Until).AddHours(12);

    [Fact]
    public void Fits_causal_history_and_evaluates_identical_holdout_against_baseline()
    {
        var loads = History();
        var evidence = Evidence();
        var report = PersonalAdaptationEngine.Build(Owner, "test", First, Split, Until, Now, 1000, loads, evidence)!;
        report.Status.Should().Be("retrospective-holdout");
        report.Model!.TrainingCount.Should().Be(6);
        report.Model.TrainingRmse.Should().BeApproximately(0, 1e-12);
        report.Holdout!.Count.Should().Be(3);
        report.BaselineHoldout!.Count.Should().Be(3);
        report.Holdout.MeanAbsoluteError.Should().BeApproximately(report.BaselineHoldout.MeanAbsoluteError, 1e-12);
        report.CurrentPredictedSpeedMetersPerSecond.Should().BeApproximately(4, 1e-12);
        report.Tests.Should().HaveCount(9);
    }

    [Fact]
    public void Refuses_missing_or_late_load_instead_of_imputing_rest()
    {
        var loads = History();
        loads[0] = loads[0] with { Load = null, AvailableAt = null };
        var report = PersonalAdaptationEngine.Build(Owner, "test", First, Split, Until, Now, 1000, loads, Evidence())!;
        report.Model.Should().BeNull();
        report.CurrentPredictedSpeedMetersPerSecond.Should().BeNull();
        loads = History();
        loads[0] = loads[0] with { AvailableAt = At(First.AddDays(5)) };
        PersonalAdaptationEngine.Build(Owner, "test", First, Split, Until, Now, 1000, loads, Evidence())!.Model.Should().BeNull();
    }

    private static DailySessionLoadPoint[] History() => Enumerable.Range(0, 20).Select(index =>
        new DailySessionLoadPoint(First.AddDays(index), "observed-rest", 0, 0, 0,
            At(First.AddDays(index + 1)), At(First.AddDays(index + 1)))).ToArray();

    private static RaceCalibrationEvidence[] Evidence() =>
        Enumerable.Range(1, 6).Select(index => Row(index, 250, "training"))
            .Concat(Enumerable.Range(13, 3).Select(index => Row(index, 260, "calibration"))).ToArray();

    private static RaceCalibrationEvidence Row(int day, double duration, string role)
    {
        var end = At(First.AddDays(day)).AddHours(12).AddSeconds(duration);
        return new RaceCalibrationEvidence(Guid.NewGuid(), 1, "hash", end, end.AddMinutes(1), 1,
            1000, duration, role, null, null, null);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
