using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class ScienceFeedbackEndpoints
{
    internal static async Task<Results<Ok<ScienceFeedbackReport>, ValidationProblem, NotFound>> GetAsync(
        DateOnly? day, Guid? workoutId, ClaimsPrincipal principal, MoscoRunDbContext db,
        AthleteFeedbackQuery query, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var now = time.GetUtcNow();
        if (!ScienceFeedbackProtocol.ValidContext(day, workoutId, now))
        {
            return Invalid();
        }

        var owner = principal.OwnerId();
        var workout = await FindWorkoutAsync(db, owner, workoutId, now, ct);
        if (workoutId.HasValue && workout is null)
        {
            return TypedResults.NotFound();
        }

        var history = await query.ReadHistoryAsync(owner, ScienceFeedbackProtocol.EntryId(day, workout), now, ct);
        return TypedResults.Ok(BuildReport(day, workout, history));
    }

    internal static async Task<Results<Ok<ScienceFeedbackReport>, ValidationProblem, NotFound, Conflict>> PutAsync(
        ScienceFeedbackRequest request, ClaimsPrincipal principal, MoscoRunDbContext db,
        AthleteFeedbackQuery query, AthleteFeedbackStore store, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var now = time.GetUtcNow();
        if (!ScienceFeedbackProtocol.ValidContext(request.Day, request.WorkoutId, now) ||
            request.Revision is < 1 or > ScienceFeedbackProtocol.MaxRevision || request.Values is null ||
            request.Note?.Length > 4000 || request.WorkoutId.HasValue != request.WorkoutRevision.HasValue)
        {
            return Invalid();
        }

        var values = ScienceFeedbackProtocol.Values(request.WorkoutId.HasValue, request.Values);
        if (values is null)
        {
            return Invalid();
        }

        var owner = principal.OwnerId();
        var workout = await FindWorkoutAsync(db, owner, request.WorkoutId, now, ct);
        if (request.WorkoutId.HasValue && workout is null)
        {
            return TypedResults.NotFound();
        }

        if (workout?.Revision != request.WorkoutRevision)
        {
            return TypedResults.Conflict();
        }

        var entryId = ScienceFeedbackProtocol.EntryId(request.Day, workout);
        var history = await query.ReadHistoryAsync(owner, entryId, now, ct);
        var latest = ScienceFeedbackProtocol.Latest(history);
        var revision = latest?.Entry.RevisionOrder ?? 0;
        if ((history.Count > 0 && latest is null) || request.Revision < revision ||
            request.Revision > revision + 1)
        {
            return TypedResults.Conflict();
        }

        var entry = new AthleteFeedbackEntry(entryId, request.Revision.ToString(CultureInfo.InvariantCulture),
            ScienceFeedbackProtocol.ReferenceTime(request.Day, workout), values, workout?.Id, request.Revision, request.Note);
        try
        {
            await store.SaveEntryAsync(owner, entry, ct);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Conflict();
        }

        history = await query.ReadHistoryAsync(owner, entryId, time.GetUtcNow(), ct);
        return TypedResults.Ok(BuildReport(request.Day, workout, history));
    }

    private static Task<Workout?> FindWorkoutAsync(MoscoRunDbContext db, Guid owner, Guid? id,
        DateTimeOffset now, CancellationToken ct) =>
        id is { } workoutId
            ? db.Workouts.AsNoTracking().SingleOrDefaultAsync(item => item.OwnerId == owner &&
                item.Id == workoutId && item.EndedAt <= now && item.UpdatedAt <= now, ct)
            : Task.FromResult<Workout?>(null);

    private static ScienceFeedbackReport BuildReport(DateOnly? day, Workout? workout,
        IReadOnlyList<AthleteFeedbackSnapshot> history)
    {
        var latest = ScienceFeedbackProtocol.Latest(history);
        var values = latest?.Entry.Values ?? ScienceFeedbackProtocol.Values(workout is not null, [])!;
        var status = latest is not null ? "recorded" : history.Count == 0 ? "missing" : "ambiguous";
        var rpe = values.SingleOrDefault(value => string.Equals(value.Metric, "rpe", StringComparison.Ordinal))?.Value;
        var load = workout?.ActiveDuration is { } duration ? TrainingLoadEngine.SessionRpe(duration.TotalMinutes, rpe) : null;
        return new ScienceFeedbackReport(day, workout?.Id, workout?.Revision, latest?.Entry.RevisionOrder,
            latest?.AvailableAt, values, latest?.Entry.Note, status, workout?.ActiveDuration?.TotalSeconds,
            load, ScienceFeedbackProtocol.LoadVersion);
    }

    private static ValidationProblem Invalid() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["feedback"] = ["Contesto, revisione, metriche o valori non validi."],
        });
}
