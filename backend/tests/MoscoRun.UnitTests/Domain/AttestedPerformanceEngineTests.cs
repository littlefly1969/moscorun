using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class AttestedPerformanceEngineTests
{
    [Fact]
    public void Estimates_only_from_training_and_distinct_duration_support()
    {
        var evidence = new[]
        {
            Row(180, 820), Row(360, 1540), Row(720, 2980),
            Row(900, 9000, "calibration"), Row(720, 5000, "late-training"),
        };
        var report = AttestedPerformanceEngine.Build(evidence);
        report.OrdinaryLeastSquares!.CriticalSpeedMetersPerSecond.Should().BeApproximately(4, 1e-9);
        report.RobustCriticalSpeed!.DistanceReserveMeters.Should().BeApproximately(100, 1e-9);
        report.RobustCriticalSpeed.EffortCount.Should().Be(3);
        report.CriticalSpeedCandidateIds.Should().HaveCount(3);
        var cooper = report.CooperTests.Should().ContainSingle().Subject;
        cooper.WorkoutId.Should().Be(evidence[2].WorkoutId);
        cooper.EstimatedVo2MaxMlKgMin.Should().BeApproximately(22.351 * 2.98 - 11.288, 1e-9);
    }

    [Fact]
    public void Refuses_unidentifiable_critical_speed_without_fabricating_cooper()
    {
        var report = AttestedPerformanceEngine.Build([Row(180, 820), Row(180, 830), Row(200, 900)]);
        report.RobustCriticalSpeed.Should().BeNull();
        report.OrdinaryLeastSquares.Should().BeNull();
        report.CooperTests.Should().BeEmpty();
    }

    private static RaceCalibrationEvidence Row(double seconds, double meters, string role = "training") =>
        new(Guid.NewGuid(), 1, "hash", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1,
            meters, seconds, role, null, null);
}
