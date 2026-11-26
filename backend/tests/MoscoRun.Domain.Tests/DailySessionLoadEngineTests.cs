using MoscoRun.Domain.Analysis;

namespace MoscoRun.Domain.Tests;

public sealed class DailySessionLoadEngineTests
{
    private static readonly DateOnly Last = new(2026, 1, 31);
    private static readonly DateTimeOffset AsOf = At(Last.AddDays(2));

    [Fact]
    public void Complete_days_compute_disjoint_windows_and_weekly_load()
    {
        var days = Enumerable.Range(0, 35).Select(i => Last.AddDays(i - 34)).ToArray();
        var attestations = days.Select(day => new DailyLoadAttestation(day, 1, At(day.AddDays(1)), false)).ToArray();
        var sessions = days.Select((day, i) => Session(day, i < 28 ? 2 : 4)).ToArray();
        var report = DailySessionLoadEngine.Build(Last, AsOf, attestations, sessions)!;
        Assert.Equal(35, report.Days.Count);
        Assert.Equal(840, report.RecentWeek!.TotalLoad);
        Assert.Equal(120, report.RecentWeek.MeanDailyLoad);
        Assert.Null(report.RecentWeek.Monotony);
        Assert.Null(report.RecentWeek.Strain);
        Assert.Equal(60, report.Rolling!.PriorMean);
        Assert.Equal(2, report.Rolling.RecentToPriorRatio);
    }

    [Fact]
    public void Confirmed_rest_is_zero_but_missing_or_ambiguous_days_are_unknown()
    {
        var report = DailySessionLoadEngine.Build(Last, AsOf,
            [new(Last, 1, At(Last.AddDays(1)), false), new(Last.AddDays(-1), null, null, true)], [])!;
        Assert.Equal("observed-rest", report.Days[^1].Status);
        Assert.Equal(0, report.Days[^1].Load);
        Assert.Equal("ambiguous-attestation", report.Days[^2].Status);
        Assert.Null(report.Days[^2].Load);
        Assert.Null(report.Days[^3].Load);
        Assert.Null(report.RecentWeek);
        Assert.Null(report.Rolling);
    }

    [Fact]
    public void Rejects_changed_sessions_missing_feedback_and_overlap()
    {
        var attestation = new DailyLoadAttestation(Last, 1, At(Last.AddDays(1)), false);
        var session = Session(Last, 4);
        DailySessionLoadPoint Point(params SessionLoadEvidence[] sessions) =>
            DailySessionLoadEngine.Build(Last, AsOf, [attestation], sessions)!.Days[^1];

        Assert.Equal("changed-after-attestation", Point(session with { UpdatedAt = AsOf }).Status);
        Assert.Null(Point(session with { Rpe = null }).Load);
        Assert.Null(Point(session with { RpeAvailableAt = AsOf.AddDays(1) }).Load);
        Assert.Equal("overlapping-sessions", Point(session, session with { WorkoutId = Guid.NewGuid() }).Status);
        Assert.Equal("cross-day-session", Point(session with { StartedAt = At(Last).AddHours(-1) }).Status);
    }

    [Fact]
    public void Early_attestation_and_open_day_do_not_produce_load()
    {
        var report = DailySessionLoadEngine.Build(Last, AsOf, [new(Last, 1, At(Last), false)], [])!;
        Assert.Equal("incomplete-attestation", report.Days[^1].Status);
        Assert.Null(report.Days[^1].Load);
        Assert.Null(DailySessionLoadEngine.Build(Last, At(Last), [], []));
    }

    private static SessionLoadEvidence Session(DateOnly day, double rpe) =>
        new(Guid.NewGuid(), 1, At(day).AddHours(8), At(day).AddHours(9), At(day).AddHours(10),
            1800, rpe, At(day).AddHours(11), false);

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
