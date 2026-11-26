using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class WorkoutReserveEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddMinutes(1);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Exhaustion_is_interpolated_within_the_native_interval()
    {
        var content = Create([new(Start, 400), new(Start.AddSeconds(5), 400), new(Start.AddSeconds(10), 400)]);
        var protocol = new WorkoutReserveProtocol(SeriesMetric.Power, 300, 750, 750, 60);
        var report = WorkoutReserveEngine.Build(Owner, content.Workout, content.Series, Now, protocol)!;
        report.Status.Should().Be("conditional-exhaustion");
        report.SupportedSeconds.Should().Be(10);
        report.EvaluatedSeconds.Should().Be(7.5);
        report.ExhaustionSeconds.Should().Be(7.5);
        report.LastRemaining.Should().Be(0);
        report.Points[^1].CumulativeSeconds.Should().Be(7.5);
    }

    [Fact]
    public void Stops_at_first_gap_without_restarting_an_unknown_reserve()
    {
        var content = Create([new(Start, 400), new(Start.AddSeconds(2), 200), new(Start.AddSeconds(10), 200)]);
        var protocol = new WorkoutReserveProtocol(SeriesMetric.Power, 300, 1000, 1000, 60);
        var report = WorkoutReserveEngine.Build(Owner, content.Workout, content.Series, Now, protocol)!;
        report.Status.Should().Be("truncated-at-gap");
        report.SupportedSeconds.Should().Be(2);
        report.LastRemaining.Should().Be(800);
        report.ExhaustionSeconds.Should().BeNull();
        WorkoutReserveEngine.Build(Guid.NewGuid(), content.Workout, content.Series, Now, protocol).Should().BeNull();
    }

    [Fact]
    public void Recovery_uses_supplied_time_constant_and_preserves_initial_state()
    {
        var content = Create([new(Start, 200), new(Start.AddSeconds(5), 200), new(Start.AddSeconds(10), 200)]);
        var protocol = new WorkoutReserveProtocol(SeriesMetric.Power, 300, 1000, 500, 20);
        var report = WorkoutReserveEngine.Build(Owner, content.Workout, content.Series, Now, protocol)!;
        report.LastRemaining.Should().BeApproximately(1000 - 500 * Math.Exp(-0.5), 1e-9);
        report.MinimumRemaining.Should().Be(500);
        report.Status.Should().Be("observed-prefix");
    }

    private static WorkoutContent Create(IReadOnlyList<SeriesSample> samples)
    {
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.Power] = samples };
        var recording = new WorkoutRecording("test", "reserve-test", 1, "RUNNING", Start, Start.AddSeconds(10),
            0, TimeSpan.FromSeconds(10), 40, null, null, null, [], series);
        return Workout.Create(Owner, Guid.NewGuid(), recording, Now).Value;
    }
}
