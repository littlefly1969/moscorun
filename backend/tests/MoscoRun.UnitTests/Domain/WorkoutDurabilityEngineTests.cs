using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class WorkoutDurabilityEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly WorkoutDurabilityProtocol Protocol = new(SeriesMetric.Speed, 0, 30, 3, 0.8, 4, 2);

    [Fact]
    public void Uses_native_samples_and_returns_a_versioned_profile_for_the_selected_protocol()
    {
        var content = Create(includeSpeed: true);
        var report = WorkoutDurabilityEngine.Build(Owner, content.Workout, content.Series, Start.AddMinutes(2), Protocol);
        report.Should().NotBeNull();
        report!.Status.Should().Be("observed");
        report.Revision.Should().Be(4);
        report.ContentSha256.Should().Be(Convert.ToHexStringLower(content.Workout.ContentHash));
        report.Protocol.Should().Be(Protocol);
        report.Windows.Should().HaveCount(3);
        report.Windows.Select(window => window.Coverage).Should().AllBeEquivalentTo(1.0);
        report.Profile.Should().NotBeNull();
        report.Profile!.LossPercent[2].Should().BeApproximately(10, 0.000001);
        report.Profile.Onset.Should().Be(Start.AddSeconds(30));
        report.Profile.OnsetConfirmedAt.Should().Be(Start.AddSeconds(90));
    }

    [Fact]
    public void Missing_channels_are_not_zero_and_wrong_owner_or_future_revision_are_rejected()
    {
        var content = Create(includeSpeed: false);
        var report = WorkoutDurabilityEngine.Build(Owner, content.Workout, content.Series, Start.AddMinutes(2), Protocol);
        report.Should().NotBeNull();
        report!.Status.Should().Be("missing-channel");
        report.Profile.Should().BeNull();
        report.Windows.Should().BeEmpty();
        WorkoutDurabilityEngine.Build(Guid.NewGuid(), content.Workout, content.Series, Start.AddMinutes(2), Protocol)
            .Should().BeNull();
        WorkoutDurabilityEngine.Build(Owner, content.Workout, content.Series, Start.AddSeconds(60), Protocol)
            .Should().BeNull();
        WorkoutDurabilityEngine.Build(Owner, content.Workout, content.Series, Start.AddMinutes(2),
            Protocol with { WindowCount = 4 }).Should().BeNull();
    }

    [Fact]
    public void Insufficient_support_preserves_windows_but_refuses_profile()
    {
        var content = Create(includeSpeed: true, sparse: true);
        var report = WorkoutDurabilityEngine.Build(Owner, content.Workout, content.Series, Start.AddMinutes(2), Protocol);
        report.Should().NotBeNull();
        report!.Status.Should().Be("insufficient-support");
        report.Profile.Should().BeNull();
        report.Windows.Should().HaveCount(3);
        report.Windows[1].Coverage.Should().BeLessThan(0.8);
    }

    private static WorkoutContent Create(bool includeSpeed, bool sparse = false)
    {
        var times = Enumerable.Range(0, 19).Select(index => index * 5).ToArray();
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>>
        {
            [SeriesMetric.HeartRate] = times.Select(offset => new SeriesSample(Start.AddSeconds(offset), 150)).ToArray(),
        };
        if (includeSpeed)
        {
            series[SeriesMetric.Speed] = times.Where(offset => !sparse || offset < 30 || offset >= 60)
                .Select(offset => new SeriesSample(Start.AddSeconds(offset), offset < 30 ? 4 : offset < 60 ? 3.8 : 3.6))
                .ToArray();
        }

        var recording = new WorkoutRecording("samsung-health", "durability-test", 4, "RUNNING", Start,
            Start.AddSeconds(90), 0, TimeSpan.FromSeconds(90), 350, null, null, null, [], series);
        return Workout.Create(Owner, Guid.NewGuid(), recording, Start.AddMinutes(2)).Value;
    }
}
