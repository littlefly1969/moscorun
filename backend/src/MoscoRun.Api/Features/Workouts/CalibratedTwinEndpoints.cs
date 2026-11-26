using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class CalibratedTwinEndpoints
{
    internal static async Task<Results<Ok<CalibratedTwinReport>, ValidationProblem, NotFound>> PostAsync(
        Guid id, CalibratedTwinRequest request, ClaimsPrincipal principal, MoscoRunDbContext db,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!CalibratedTwinEngine.Valid(request))
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

        var calibration = WorkoutCardiacRecoveryEngine.Build(owner, snapshot.Workout, snapshot.Series, asOf,
            request.RecoveryProtocol);
        if (calibration is null)
        {
            return Invalid();
        }

        var report = CalibratedTwinEngine.Build(request, calibration, snapshot.Workout.UpdatedAt, asOf);
        return report is not null ? TypedResults.Ok(report) : Invalid();
    }

    private static ValidationProblem Invalid() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["scenario"] = ["Protocollo di recupero, limiti FC e segmenti dello scenario devono essere validi."],
        });
}
