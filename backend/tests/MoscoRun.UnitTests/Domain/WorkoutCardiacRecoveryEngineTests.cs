using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class WorkoutCardiacRecoveryEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddMinutes(5);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly WorkoutCardiacRecoveryProtocol Protocol = new(30, "passive-seated", true, 70);

    [Fact]
    public void Native_recovery_points_and_descriptive_fit_are_protocol_specific()
    {
        var content = Create();
        var report = WorkoutCardiacRecoveryEngine.Build(Owner, content.Workout, content.Series, Now, Protocol)!;
        report.Status.Should().Be("observed");
        report.Points[0].HeartRateBpm.Should().Be(170);
        report.Points[1].DropFromEndBpm.Should().BeApproximately(100 * (1 - Math.Exp(-2)), 0.001);
        report.Points[2].DropFromEndBpm.Should().BeApproximately(100 * (1 - Math.Exp(-4)), 0.001);
        report.Fit!.TimeConstantSeconds.Should().Be(30);
        report.Fit.RootMeanSquaredErrorBpm.Should().BeLessThan(0.001);
    }

    [Fact]
    public void Missing_target_is_not_replaced_with_future_sample_and_gaps_refuse_fit()
    {
        var content = Create(gap: true);
        var report = WorkoutCardiacRecoveryEngine.Build(Owner, content.Workout, content.Series, Now, Protocol)!;
        report.Status.Should().Be("partial-support");
        report.Points[1].HeartRateBpm.Should().BeNull();
        report.Points[1].ObservedAt.Should().BeNull();
        report.Points[2].HeartRateBpm.Should().NotBeNull();
        report.Fit.Should().BeNull();
    }

    [Fact]
    public void Refuses_unconfirmed_protocol_owner_and_future_revision()
    {
        var content = Create();
        var report = WorkoutCardiacRecoveryEngine.Build(Owner, content.Workout, content.Series, Now,
            Protocol with { Confirmed = false })!;
        report.Status.Should().Be("unconfirmed-protocol");
        report.Points.Should().OnlyContain(point => point.HeartRateBpm == null);
        report.Fit.Should().BeNull();
        WorkoutCardiacRecoveryEngine.Build(Guid.NewGuid(), content.Workout, content.Series, Now, Protocol).Should().BeNull();
        WorkoutCardiacRecoveryEngine.Build(Owner, content.Workout, content.Series, Now.AddSeconds(-1), Protocol).Should().BeNull();
    }

    private static WorkoutContent Create(bool gap = false)
    {
        var samples = Enumerable.Range(0, 25).Select(index => index * 5)
            .Where(seconds => !gap || seconds < 55 || seconds > 60)
            .Select(seconds => new SeriesSample(Start.AddSeconds(30 + seconds), 70 + 100 * Math.Exp(-seconds / 30.0)))
            .ToArray();
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.HeartRate] = samples };
        var recording = new WorkoutRecording("samsung-health", "cardiac-recovery", 1, "RUNNING", Start,
            Start.AddSeconds(150), 0, TimeSpan.FromSeconds(150), 500, null, null, null, [], series);
        return Workout.Create(Owner, Guid.NewGuid(), recording, Now).Value;
    }
}
