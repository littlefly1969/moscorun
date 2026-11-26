using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class WorkoutIntensityEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddMinutes(2);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly WorkoutIntensityProtocol Protocol = new(SeriesMetric.HeartRate, [140, 160], 60, 200, 1, 2);

    [Fact]
    public void Distribution_keeps_gaps_separate_and_uses_observed_minutes_for_cardiac_load()
    {
        var content = Create([new(Start, 120), new(Start.AddSeconds(5), 140),
            new(Start.AddSeconds(15), 160), new(Start.AddSeconds(20), 160)]);
        var report = WorkoutIntensityEngine.Build(Owner, content.Workout, content.Series, Now, Protocol)!;
        report.Status.Should().Be("partial-support");
        report.Revision.Should().Be(1);
        report.Distribution.ZoneMinutes.Should().AllSatisfy(minutes => minutes.Should().BeApproximately(5d / 60, 1e-10));
        report.Distribution.MissingMinutes.Should().BeApproximately(5d / 60, 1e-10);
        report.Distribution.Coverage.Should().BeApproximately(0.75, 1e-10);
        report.ObservedMean.Should().BeApproximately(140, 1e-10);
        var expected = 0.25 * (140 - 60) / (200 - 60);
        report.SupportedHeartRateReserveLoad.Should().BeApproximately(expected, 1e-10);
        report.SupportedExponentialTrimp.Should().BeApproximately(expected * Math.Exp(2 * expected / 0.25), 1e-10);
    }

    [Fact]
    public void Missing_channel_and_out_of_calibration_never_invent_load()
    {
        var content = Create([new(Start, 120), new(Start.AddSeconds(5), 180),
            new(Start.AddSeconds(10), 120), new(Start.AddSeconds(15), 120), new(Start.AddSeconds(20), 120)]);
        var outside = WorkoutIntensityEngine.Build(Owner, content.Workout, content.Series, Now,
            Protocol with { MaximumBpm = 160 })!;
        outside.HeartRateStatus.Should().Be("outside-calibration");
        outside.SupportedHeartRateReserveLoad.Should().BeNull();
        outside.SupportedExponentialTrimp.Should().BeNull();
        var missing = WorkoutIntensityEngine.Build(Owner, content.Workout, [], Now, Protocol)!;
        missing.Status.Should().Be("missing-channel");
        missing.Distribution.Coverage.Should().Be(0);
        missing.ObservedMean.Should().BeNull();
        missing.SupportedHeartRateReserveLoad.Should().BeNull();
    }

    [Fact]
    public void Rejects_invalid_protocol_owner_and_future_snapshot()
    {
        var content = Create([new(Start, 140), new(Start.AddSeconds(5), 140)]);
        WorkoutIntensityEngine.Build(Guid.NewGuid(), content.Workout, content.Series, Now, Protocol).Should().BeNull();
        WorkoutIntensityEngine.Build(Owner, content.Workout, content.Series, Now.AddSeconds(-1), Protocol).Should().BeNull();
        (Protocol with { UpperBoundaries = [160, 140] }).IsValid().Should().BeFalse();
        (Protocol with { RestingBpm = null }).IsValid().Should().BeFalse();
        (Protocol with { TrimpB = null }).IsValid().Should().BeFalse();
        (Protocol with { Metric = SeriesMetric.Speed }).IsValid().Should().BeFalse();
    }

    private static WorkoutContent Create(IReadOnlyList<SeriesSample> samples)
    {
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.HeartRate] = samples };
        var recording = new WorkoutRecording("samsung-health", "intensity-test", 1, "RUNNING", Start,
            Start.AddSeconds(20), 0, TimeSpan.FromSeconds(20), 100, null, null, null, [], series);
        return Workout.Create(Owner, Guid.NewGuid(), recording, Now).Value;
    }
}
