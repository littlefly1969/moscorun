using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutCardiacRecoveryEndpoints
{
    internal static async Task<Results<Ok<WorkoutCardiacRecoveryReport>, ValidationProblem, NotFound>> GetAsync(
        Guid id, int endOffsetSeconds, string mode, bool confirmed, double? equilibriumBpm,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var protocol = new WorkoutCardiacRecoveryProtocol(endOffsetSeconds, mode, confirmed, equilibriumBpm);
        if (!protocol.IsValid())
        {
            return Invalid();
        }

        var owner = principal.OwnerId();
        var asOf = time.GetUtcNow();
        var snapshot = await db.Workouts.AsNoTracking().AsSingleQuery()
            .Where(workout => workout.Id == id && workout.OwnerId == owner)
            .Select(workout => new
            {
                Workout = workout,
                Series = db.WorkoutSeries.Where(series => series.WorkoutId == workout.Id &&
                    series.Metric == SeriesMetric.HeartRate).ToList(),
            }).SingleOrDefaultAsync(ct);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        if (endOffsetSeconds > (snapshot.Workout.EndedAt - snapshot.Workout.StartedAt).TotalSeconds)
        {
            return Invalid();
        }

        var report = WorkoutCardiacRecoveryEngine.Build(owner, snapshot.Workout, snapshot.Series, asOf, protocol);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }

    private static ValidationProblem Invalid() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["protocol"] = ["Fine sforzo entro la seduta, modalità di recupero e FC di equilibrio valide."],
        });
}
