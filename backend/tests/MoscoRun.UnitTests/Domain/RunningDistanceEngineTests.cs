using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class RunningDistanceEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = DateOnly.FromDateTime(Start.UtcDateTime);

    [Fact]
    public void Compares_against_longest_session_not_daily_total()
    {
        var current = Run(Start, 12000);
        var prior = new[] { Run(Start.AddDays(-2), 8000), Run(Start.AddDays(-2).AddHours(2), 6000) };
        var report = RunningDistanceEngine.Build(current, Attestations(), prior, Start.AddHours(1))!;
        report.Status.Should().Be("observed");
        report.CompleteDays.Should().Be(30);
        report.PriorLongestMeters.Should().Be(8000);
        report.IncreasePercent.Should().Be(50);
        report.Days.Single(day => day.Day == Day.AddDays(-2)).TotalMeters.Should().Be(14000);
    }

    [Fact]
    public void Missing_attestations_late_revisions_and_mixed_sources_refuse_comparison()
    {
        var current = Run(Start, 12000);
        var prior = Run(Start.AddDays(-2), 8000);
        var attestations = Attestations();
        var missing = RunningDistanceEngine.Build(current, attestations.Skip(1).ToArray(), [prior], Start.AddHours(1))!;
        missing.PriorLongestMeters.Should().BeNull();
        missing.IncreasePercent.Should().BeNull();
        var late = RunningDistanceEngine.Build(current, attestations,
            [prior with { AvailableAt = Start.AddMinutes(1) }], Start.AddHours(1))!;
        late.IncreasePercent.Should().BeNull();
        var mixed = RunningDistanceEngine.Build(current, attestations, [prior with { Source = "other" }], Start.AddHours(1))!;
        mixed.IncreasePercent.Should().BeNull();
        var rest = RunningDistanceEngine.Build(current, attestations, [], Start.AddHours(1))!;
        rest.Status.Should().Be("no-prior-running-session");
        rest.IncreasePercent.Should().BeNull();
    }

    private static DailyLoadAttestation[] Attestations() => Enumerable.Range(1, 30).Select(offset =>
    {
        var day = Day.AddDays(-offset);
        return new DailyLoadAttestation(day, 1,
            new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddMinutes(1), false);
    }).ToArray();

    private static RunningDistanceEvidence Run(DateTimeOffset start, double distance) =>
        new(Guid.NewGuid(), 1, "hash", "test", "RUNNING", start, start.AddMinutes(30), start.AddMinutes(31), distance);
}
