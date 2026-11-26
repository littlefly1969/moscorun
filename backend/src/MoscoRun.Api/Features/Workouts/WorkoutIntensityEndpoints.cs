using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutIntensityEndpoints
{
    internal static async Task<Results<Ok<WorkoutIntensityReport>, ValidationProblem, NotFound>> GetAsync(
        Guid id, SeriesMetric metric, string thresholds, double? restingBpm, double? maximumBpm,
        double? trimpA, double? trimpB, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time,
        HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var owner = principal.OwnerId();
        var asOf = time.GetUtcNow();
        var boundaries = Parse(thresholds);
        if (boundaries is null)
        {
            return Invalid();
        }

        var protocol = new WorkoutIntensityProtocol(metric, boundaries, restingBpm, maximumBpm, trimpA, trimpB);
        if (!protocol.IsValid())
        {
            return Invalid();
        }

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

        var report = WorkoutIntensityEngine.Build(owner, snapshot.Workout, snapshot.Series, asOf, protocol);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }

    private static ReadOnlyCollection<double>? Parse(string thresholds)
    {
        if (string.IsNullOrWhiteSpace(thresholds) || thresholds.Length > 200)
        {
            return null;
        }

        var parts = thresholds.Split(',');
        if (parts.Length is < 1 or > 10)
        {
            return null;
        }

        var values = new double[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]))
            {
                return null;
            }
        }

        return Array.AsReadOnly(values);
    }

    private static ValidationProblem Invalid() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["protocol"] = ["Soglie crescenti separate da virgole (punto decimale) e parametri FC coerenti."],
        });
}
