using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class SleepPerformanceEngineTests
{
    private static readonly DateOnly First = new(2026, 11, 1);
    private static readonly DateOnly Last = First.AddDays(13);
    private static readonly DateTimeOffset Now = At(Last.AddDays(1));

    [Fact]
    public void Correlation_uses_only_homogeneous_causal_pairs()
    {
        var nights = Enumerable.Range(0, 14).Select(index => Night(First.AddDays(index - 1), index)).ToArray();
        var tests = Enumerable.Range(0, 8).Select(index => Test(index, 3 + index * 0.1)).ToArray();
        var report = SleepPerformanceEngine.Build("test", Last, 14, 1, Now, 1000, nights, tests)!;
        report.PairedTests.Should().Be(8);
        report.SleepHoursCorrelation.Should().BeApproximately(1, 1e-9);
        report.SleepEfficiencyCorrelation.Should().BeApproximately(1, 1e-9);
        report.Evidence.Should().OnlyContain(item => item.SleepAvailableAt <= item.TestStartedAt);
    }

    [Fact]
    public void Late_revisions_duplicate_test_days_and_other_distances_do_not_contribute()
    {
        var nights = Enumerable.Range(0, 14).Select(index => Night(First.AddDays(index - 1), index)).ToArray();
        nights[0] = nights[0] with { AvailableAt = At(First.AddDays(1)) };
        var tests = Enumerable.Range(0, 8).Select(index => Test(index, 3 + index * 0.1))
            .Append(Test(1, 4)).Append(Test(9, 4) with { DistanceMeters = 2000 }).ToArray();
        var report = SleepPerformanceEngine.Build("test", Last, 14, 1, Now, 1000, nights, tests)!;
        report.PairedTests.Should().Be(6);
        report.SleepHoursCorrelation.Should().BeNull();
        report.Evidence.Should().ContainSingle(item => string.Equals(item.Status, "late-night-revision", StringComparison.Ordinal));
        report.Evidence.Count(item => string.Equals(item.Status, "multiple-tests-per-day", StringComparison.Ordinal)).Should().Be(2);
    }

    private static SleepDiaryNight Night(DateOnly day, int index) =>
        new(day, "observed", 1, At(day.AddDays(1)), 1380, 480, (6 + index * 0.1) * 3600, 32400, 70 + index);

    private static RaceCalibrationEvidence Test(int day, double speed)
    {
        var end = At(First.AddDays(day)).AddHours(12);
        return new RaceCalibrationEvidence(Guid.NewGuid(), 1, "hash", end, end.AddMinutes(1), 1,
            1000, 1000 / speed, "calibration", null, null, null);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
