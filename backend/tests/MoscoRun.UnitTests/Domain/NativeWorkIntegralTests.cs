using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class NativeWorkIntegralTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Integrates_native_power_over_exact_complete_support()
    {
        var content = Create([new(Start, 100), new(Start.AddSeconds(2), 300),
            new(Start.AddSeconds(5), 200), new(Start.AddSeconds(10), 500)]);
        NativeWorkIntegral.MeanPower(content.Workout, content.Series.Single()).Should().Be(210);
    }

    [Fact]
    public void Refuses_missing_boundaries_or_gaps_and_does_not_extrapolate()
    {
        var gap = Create([new(Start, 100), new(Start.AddSeconds(6), 300), new(Start.AddSeconds(10), 200)]);
        NativeWorkIntegral.MeanPower(gap.Workout, gap.Series.Single()).Should().BeNull();
        var truncated = Create([new(Start, 100), new(Start.AddSeconds(5), 300)]);
        NativeWorkIntegral.MeanPower(truncated.Workout, truncated.Series.Single()).Should().BeNull();
    }

    private static WorkoutContent Create(IReadOnlyList<SeriesSample> samples)
    {
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.Power] = samples };
        var recording = new WorkoutRecording("test", "power-test", 1, "RUNNING", Start, Start.AddSeconds(10),
            0, TimeSpan.FromSeconds(10), 40, null, null, null, [], series);
        return Workout.Create(Guid.NewGuid(), Guid.NewGuid(), recording, Start.AddMinutes(1)).Value;
    }
}
