using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class PacingScenarioEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Declared_time_is_allocated_by_distance_and_relative_cost()
    {
        var request = new PacingScenarioRequest([new(1000, 1), new(1000, 1.5)], 600, null);
        var report = PacingScenarioEngine.Build(request, Now, null)!;
        report.Status.Should().Be("conditional-scenario");
        report.Splits[0].SegmentSeconds.Should().Be(240);
        report.Splits[1].SegmentSeconds.Should().Be(360);
        report.Splits[^1].CumulativeSeconds.Should().Be(600);
        report.Prediction.Should().BeNull();
        PacingScenarioEngine.Build(request with { Segments = [new(1000, 0)] }, Now, null).Should().BeNull();
    }

    [Fact]
    public void Missing_personal_model_never_becomes_a_declared_target()
    {
        var day = DateOnly.FromDateTime(Now.UtcDateTime);
        var reference = new PacingRaceReference("test", day.AddDays(-30), day.AddDays(-10), day);
        var request = new PacingScenarioRequest([new(2000, 1)], null, reference);
        var owner = Guid.NewGuid();
        var race = AttestedRaceEngine.Build(owner, reference.Source, At(reference.From), At(reference.Split),
            At(reference.Until), Now, 2000, [])!;
        var report = PacingScenarioEngine.Build(request, Now, race)!;
        report.Status.Should().Be("insufficient-calibration");
        report.TargetSeconds.Should().BeNull();
        report.Splits.Should().BeEmpty();
        PacingScenarioEngine.Build(request with { TargetSeconds = 600 }, Now, race).Should().BeNull();
        PacingScenarioEngine.Build(request, Now, race with { TargetMeters = 3000 }).Should().BeNull();
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
