using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class PacingScenarioEndpoints
{
    internal static async Task<Results<Ok<PacingScenarioReport>, ValidationProblem>> PostAsync(
        PacingScenarioRequest request, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time,
        HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var asOf = time.GetUtcNow();
        if (!PacingScenarioEngine.Valid(request, asOf))
        {
            return Invalid();
        }

        var race = request.RaceReference is { } reference
            ? await new AttestedRaceReader(db).ReadAsync(principal.OwnerId(), reference.Source, At(reference.From),
                At(reference.Split), At(reference.Until), asOf, request.Segments.Sum(item => item.DistanceMeters), ct)
            : null;
        var report = PacingScenarioEngine.Build(request, asOf, race);
        return report is not null ? TypedResults.Ok(report) : Invalid();
    }

    private static ValidationProblem Invalid() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["scenario"] = ["Indicare segmenti validi e un solo obiettivo: tempo dichiarato oppure calibrazione personale."],
        });

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
