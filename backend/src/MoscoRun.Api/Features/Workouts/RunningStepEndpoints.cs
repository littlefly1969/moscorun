using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class RunningStepEndpoints
{
    internal static async Task<Results<Ok<RunningStepReport>, NotFound>> GetAsync(
        Guid id, bool cadenceIsStepsPerMinute, ClaimsPrincipal principal, MoscoRunDbContext db,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var owner = principal.OwnerId();
        var asOf = time.GetUtcNow();
        var snapshot = await db.Workouts.AsNoTracking().AsSingleQuery()
            .Where(workout => workout.Id == id && workout.OwnerId == owner)
            .Select(workout => new
            {
                Workout = workout,
                Series = db.WorkoutSeries.Where(series => series.WorkoutId == workout.Id &&
                    (series.Metric == SeriesMetric.Speed || series.Metric == SeriesMetric.Cadence)).ToList(),
            }).SingleOrDefaultAsync(ct);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        var report = RunningStepEngine.Build(owner, snapshot.Workout, snapshot.Series, asOf, cadenceIsStepsPerMinute);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }
}
