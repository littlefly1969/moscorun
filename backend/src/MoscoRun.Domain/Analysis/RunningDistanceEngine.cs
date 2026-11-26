namespace MoscoRun.Domain.Analysis;

/// <summary>Confronto descrittivo con la seduta più lunga dei 30 giorni UTC precedenti, non rischio di infortunio.</summary>
public static class RunningDistanceEngine
{
    public const string MethodVersion = "running-distance-prior-30-completed-utc-days-v1";
    public const int PriorDays = 30;

    public static RunningDistanceReport? Build(RunningDistanceEvidence current, IReadOnlyList<DailyLoadAttestation> attestations,
        IReadOnlyList<RunningDistanceEvidence> sessions, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(attestations);
        ArgumentNullException.ThrowIfNull(sessions);
        var day = DateOnly.FromDateTime(current.StartedAt.UtcDateTime);
        if (day.DayNumber < PriorDays || current.AvailableAt > asOf || current.EndedAt > asOf ||
            current.EndedAt <= current.StartedAt || !IsRun(current) || !ValidDistance(current.DistanceMeters) ||
            sessions.Any(item => item.WorkoutId == Guid.Empty || item.WorkoutId == current.WorkoutId ||
                item.EndedAt <= item.StartedAt) ||
            sessions.Select(item => item.WorkoutId).Distinct().Count() != sessions.Count ||
            attestations.Select(item => item.Day).Distinct().Count() != attestations.Count)
        {
            return null;
        }

        var first = day.AddDays(-PriorDays);
        var last = day.AddDays(-1);
        var cutoff = current.StartedAt;
        var byDay = attestations.ToDictionary(item => item.Day);
        var days = Enumerable.Range(0, PriorDays).Select(offset => first.AddDays(offset)).Select(date =>
            Day(date, current.Source, cutoff, byDay.GetValueOrDefault(date), sessions)).ToArray();
        var complete = days.Count(item => item.TotalMeters.HasValue);
        var runs = sessions.Where(item => IsRun(item) && item.StartedAt >= At(first) && item.EndedAt <= At(day) &&
            item.AvailableAt <= cutoff && string.Equals(item.Source, current.Source, StringComparison.Ordinal) &&
            ValidDistance(item.DistanceMeters)).OrderBy(item => item.StartedAt).ToArray();
        var longest = complete == PriorDays ? days.Max(item => item.LongestMeters) : null;
        var increase = TrainingLoadEngine.DistanceIncreasePercent(current.DistanceMeters.GetValueOrDefault(), longest);
        var status = complete < PriorDays ? "incomplete-prior-history" : longest is null ? "no-prior-running-session" : "observed";
        return new RunningDistanceReport(MethodVersion, asOf, cutoff, first, last, current, status, complete,
            Array.AsReadOnly(days), Array.AsReadOnly(runs), longest, increase);
    }

    private static DailyRunningDistancePoint Day(DateOnly day, string source, DateTimeOffset cutoff,
        DailyLoadAttestation? attestation, IReadOnlyList<RunningDistanceEvidence> sessions)
    {
        var start = At(day);
        var end = start.AddDays(1);
        var overlapping = sessions.Where(item => item.StartedAt < end && item.EndedAt > start)
            .OrderBy(item => item.StartedAt).ToArray();
        var runs = overlapping.Where(IsRun).ToArray();
        var available = attestation?.AvailableAt;
        var status = Failure(attestation, overlapping, runs, start, end, cutoff, source);
        if (status is not null)
        {
            return new DailyRunningDistancePoint(day, status, runs.Length, null, null, available);
        }

        return new DailyRunningDistancePoint(day, runs.Length == 0 ? "observed-rest" : "observed",
            runs.Length, runs.Sum(item => item.DistanceMeters.GetValueOrDefault()),
            runs.Length == 0 ? null : runs.Max(item => item.DistanceMeters), available);
    }

    private static string? Failure(DailyLoadAttestation? attestation, RunningDistanceEvidence[] sessions,
        RunningDistanceEvidence[] runs, DateTimeOffset start, DateTimeOffset end, DateTimeOffset cutoff, string source)
    {
        if (attestation?.Ambiguous == true)
        {
            return "ambiguous-attestation";
        }

        if (attestation?.AvailableAt is not { } available || available < end || available > cutoff || attestation.Complete != 1)
        {
            return "missing-or-incomplete-attestation";
        }

        if (sessions.Any(item => item.StartedAt < start || item.EndedAt > end))
        {
            return "cross-day-session";
        }

        if (sessions.Any(item => item.AvailableAt > available || item.AvailableAt > cutoff))
        {
            return "changed-after-attestation";
        }

        for (var index = 1; index < sessions.Length; index++)
        {
            if (sessions[index].StartedAt < sessions[index - 1].EndedAt)
            {
                return "overlapping-sessions";
            }
        }

        if (runs.Any(item => !string.Equals(item.Source, source, StringComparison.Ordinal)))
        {
            return "incompatible-running-source";
        }

        return runs.Any(item => !ValidDistance(item.DistanceMeters)) ? "missing-distance" : null;
    }

    private static bool IsRun(RunningDistanceEvidence item) => string.Equals(item.ActivityType, "RUNNING", StringComparison.OrdinalIgnoreCase);

    private static bool ValidDistance(double? distance) => distance is { } meters && double.IsFinite(meters) && meters > 0;

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
