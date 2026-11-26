namespace MoscoRun.Domain.Analysis;

/// <summary>Somma session-RPE soltanto per giornate dichiarate complete dopo la loro conclusione.</summary>
public static class DailySessionLoadEngine
{
    public const string MethodVersion = "attested-daily-session-rpe-v1";
    public const int RecentDays = 7;
    public const int PriorDays = 28;

    public static bool IsValidLastDay(DateOnly last, DateTimeOffset asOf) =>
        last.DayNumber >= RecentDays + PriorDays - 1 && last < DateOnly.FromDateTime(asOf.UtcDateTime);

    public static DailySessionLoadReport? Build(DateOnly last, DateTimeOffset asOf,
        IReadOnlyList<DailyLoadAttestation> attestations, IReadOnlyList<SessionLoadEvidence> sessions)
    {
        ArgumentNullException.ThrowIfNull(attestations);
        ArgumentNullException.ThrowIfNull(sessions);
        if (!IsValidLastDay(last, asOf) || sessions.Any(session => session.WorkoutId == Guid.Empty ||
                session.EndedAt <= session.StartedAt) ||
            sessions.Select(session => session.WorkoutId).Distinct().Count() != sessions.Count ||
            attestations.Select(item => item.Day).Distinct().Count() != attestations.Count)
        {
            return null;
        }

        var first = last.AddDays(1 - RecentDays - PriorDays);
        var byDay = attestations.ToDictionary(item => item.Day);
        var points = new List<DailySessionLoadPoint>(RecentDays + PriorDays);
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var start = At(day);
            var end = start.AddDays(1);
            var overlapping = sessions.Where(session => session.StartedAt < end && session.EndedAt > start)
                .OrderBy(session => session.StartedAt).ToArray();
            byDay.TryGetValue(day, out var attestation);
            points.Add(BuildDay(day, asOf, attestation, overlapping));
        }

        var loads = points.Select(point => new DailyTrainingLoad(point.Day, point.AvailableAt ?? asOf, point.Load)).ToArray();
        var week = TrainingLoadEngine.SummarizeWeek(loads.TakeLast(RecentDays).Select(item => item.Load).ToArray());
        var rolling = RollingTrainingLoad.Compare(loads, last, asOf, RecentDays, PriorDays);
        return new DailySessionLoadReport(last, asOf, MethodVersion, "UA", points.AsReadOnly(), week, rolling);
    }

    private static DailySessionLoadPoint BuildDay(DateOnly day, DateTimeOffset asOf,
        DailyLoadAttestation? attestation, SessionLoadEvidence[] sessions)
    {
        var loads = sessions.Select(session => Load(session, asOf)).ToArray();
        var rated = loads.Count(load => load.HasValue);
        var available = attestation?.AvailableAt;
        string? failure = AttestationFailure(day, asOf, attestation);
        if (failure is null)
        {
            failure = SessionFailure(day, sessions, available.GetValueOrDefault(), asOf);
        }

        if (failure is not null || rated != sessions.Length)
        {
            return new DailySessionLoadPoint(day, failure ?? "missing-session-feedback", sessions.Length,
                rated, null, null, available);
        }

        var total = loads.Sum(load => load.GetValueOrDefault());
        var lastInput = sessions.Select(session => session.RpeAvailableAt.GetValueOrDefault())
            .Append(available.GetValueOrDefault()).Max();
        return double.IsFinite(total)
            ? new DailySessionLoadPoint(day, sessions.Length == 0 ? "observed-rest" : "observed",
                sessions.Length, rated, total, lastInput, available)
            : new DailySessionLoadPoint(day, "invalid-load", sessions.Length, rated, null, null, available);
    }

    private static string? AttestationFailure(DateOnly day, DateTimeOffset asOf, DailyLoadAttestation? item)
    {
        if (item?.Ambiguous == true)
        {
            return "ambiguous-attestation";
        }

        if (item is null || item.AvailableAt is not { } available || available > asOf)
        {
            return "missing-attestation";
        }

        if (item.Complete != 1 || available < At(day).AddDays(1))
        {
            return "incomplete-attestation";
        }

        return null;
    }

    private static string? SessionFailure(DateOnly day, SessionLoadEvidence[] sessions,
        DateTimeOffset attestedAt, DateTimeOffset asOf)
    {
        if (sessions.Any(session => session.UpdatedAt > attestedAt || session.UpdatedAt > asOf))
        {
            return "changed-after-attestation";
        }

        var start = At(day);
        var end = start.AddDays(1);
        if (sessions.Any(session => session.StartedAt < start || session.EndedAt > end))
        {
            return "cross-day-session";
        }

        for (var index = 1; index < sessions.Length; index++)
        {
            if (sessions[index].StartedAt < sessions[index - 1].EndedAt)
            {
                return "overlapping-sessions";
            }
        }

        return null;
    }

    private static double? Load(SessionLoadEvidence session, DateTimeOffset asOf) =>
        !session.FeedbackAmbiguous && session.RpeAvailableAt is { } available && available <= asOf &&
        available >= session.UpdatedAt && available >= session.EndedAt && session.ActiveDurationSeconds is { } seconds
            ? TrainingLoadEngine.SessionRpe(seconds / 60, session.Rpe)
            : null;

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
