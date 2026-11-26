using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class RunningStepEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddMinutes(2);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Pairs_asynchronous_signals_and_weights_step_length_by_common_time()
    {
        var content = Create();
        var report = RunningStepEngine.Build(Owner, content.Workout, content.Series, Now, true)!;
        report.Status.Should().Be("observed");
        report.PairedSeconds.Should().Be(8);
        report.Coverage.Should().BeApproximately(0.4, 1e-10);
        report.MeanSpeedMetersPerSecond.Should().BeApproximately(3.625, 1e-10);
        report.MeanCadenceStepsPerMinute.Should().BeApproximately(161.25, 1e-10);
        report.MeanStepLengthMeters.Should().BeApproximately(1.35, 1e-10);
        report.StepLengthCvPercent.Should().BeGreaterThan(0);
        report.CadenceCvPercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Rejects_other_owners_future_revisions_and_unconfirmed_cadence()
    {
        var content = Create();
        RunningStepEngine.Build(Guid.NewGuid(), content.Workout, content.Series, Now, true).Should().BeNull();
        RunningStepEngine.Build(Owner, content.Workout, content.Series, Now.AddSeconds(-1), true).Should().BeNull();
        var unknown = RunningStepEngine.Build(Owner, content.Workout, content.Series, Now, false)!;
        unknown.Status.Should().Be("unconfirmed-cadence-unit");
        unknown.MeanStepLengthMeters.Should().BeNull();
        unknown.PairedSeconds.Should().Be(0);
        var missing = RunningStepEngine.Build(Owner, content.Workout, [], Now, true)!;
        missing.Status.Should().Be("missing-channel");
        missing.CadenceCvPercent.Should().BeNull();
    }

    private static WorkoutContent Create()
    {
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>>
        {
            [SeriesMetric.Speed] = [new(Start, 3), new(Start.AddSeconds(5), 4), new(Start.AddSeconds(10), 4)],
            [SeriesMetric.Cadence] = [new(Start.AddSeconds(2), 150), new(Start.AddSeconds(7), 180),
                new(Start.AddSeconds(12), 180)],
        };
        var recording = new WorkoutRecording("samsung-health", "paired-steps", 1, "RUNNING", Start,
            Start.AddSeconds(20), 0, TimeSpan.FromSeconds(20), 80, null, null, null, [], series);
        return Workout.Create(Owner, Guid.NewGuid(), recording, Now).Value;
    }
}
