using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutScienceEndpoints
{
    internal static async Task<Results<Ok<WorkoutScienceReport>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        var ownerId = principal.OwnerId();
        var asOf = time.GetUtcNow();

        // Un'unica query garantisce che genitore e serie provengano dalla stessa snapshot PostgreSQL.
        var snapshot = await db.Workouts.AsNoTracking().AsSingleQuery()
            .Where(workout => workout.Id == id && workout.OwnerId == ownerId)
            .Select(workout => new
            {
                Workout = workout,
                Series = db.WorkoutSeries.Where(series => series.WorkoutId == workout.Id).ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        var report = WorkoutScientificEngine.Build(ownerId, snapshot.Workout, snapshot.Series, asOf);
        http.Response.Headers.CacheControl = "private, no-store";
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }
}
