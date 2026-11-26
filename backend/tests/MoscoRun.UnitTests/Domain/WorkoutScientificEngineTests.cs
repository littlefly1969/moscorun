using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class WorkoutScientificEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Report_preserves_missing_channels_and_revision()
    {
        var content = Create();
        var report = WorkoutScientificEngine.Build(Owner, content.Workout, content.Series, Now);
        report.Should().NotBeNull();
        report!.Revision.Should().Be(3);
        report.Channels.Should().HaveCount(4);
        var heart = report.Channels.Single(channel => channel.Metric == SeriesMetric.HeartRate);
        heart.Status.Should().Be("observed");
        heart.TimeWeightedMean.Should().Be(140);
        heart.Coverage.Should().Be(1);
        var power = report.Channels.Single(channel => channel.Metric == SeriesMetric.Power);
        power.Status.Should().Be("missing");
        power.TimeWeightedMean.Should().BeNull();
    }

    [Fact]
    public void Report_refuses_other_owner_or_future_revision()
    {
        var content = Create();
        WorkoutScientificEngine.Build(Guid.NewGuid(), content.Workout, content.Series, Now).Should().BeNull();
        WorkoutScientificEngine.Build(Owner, content.Workout, content.Series, Now.AddMinutes(-1)).Should().BeNull();
    }

    private static WorkoutContent Create()
    {
        var start = Now.AddMinutes(-10);
        var end = start.AddSeconds(10);
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>>
        {
            [SeriesMetric.HeartRate] = [new(start, 140), new(start.AddSeconds(5), 140), new(end, 140)],
        };
        var recording = new WorkoutRecording("samsung-health", "science-test", 3, "RUNNING", start, end,
            0, TimeSpan.FromSeconds(10), 40, null, null, null, [], series);
        return Workout.Create(Owner, Guid.NewGuid(), recording, Now).Value;
    }
}
