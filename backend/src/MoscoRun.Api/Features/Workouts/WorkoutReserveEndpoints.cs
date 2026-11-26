using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutReserveEndpoints
{
    internal static async Task<Results<Ok<WorkoutReserveReport>, ValidationProblem, NotFound>> GetAsync(
        Guid id, SeriesMetric metric, double criticalIntensity, double capacity, double initialRemaining,
        double recoveryTimeConstantSeconds, ClaimsPrincipal principal, MoscoRunDbContext db,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var protocol = new WorkoutReserveProtocol(metric, criticalIntensity, capacity, initialRemaining, recoveryTimeConstantSeconds);
        if (!protocol.IsValid())
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Soglia, riserva iniziale, capacità e costante di recupero devono essere coerenti e positive."],
            });
        }

        var owner = principal.OwnerId();
        var asOf = time.GetUtcNow();
        var snapshot = await db.Workouts.AsNoTracking().AsSingleQuery()
            .Where(workout => workout.Id == id && workout.OwnerId == owner)
            .Select(workout => new
            {
                Workout = workout,
                Series = db.WorkoutSeries.Where(series => series.WorkoutId == workout.Id && series.Metric == metric).ToList(),
            }).SingleOrDefaultAsync(ct);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        var report = WorkoutReserveEngine.Build(owner, snapshot.Workout, snapshot.Series, asOf, protocol);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }
}
