using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class PersonalAdaptationEndpoints
{
    internal static async Task<Results<Ok<PersonalAdaptationReport>, ValidationProblem>> GetAsync(
        string source, DateOnly from, DateOnly split, DateOnly until, double distanceMeters,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new PersonalAdaptationReader(db).ReadAsync(principal.OwnerId(), source,
            from, split, until, time.GetUtcNow(), distanceMeters, ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Finestra UTC fino a 180 giorni, blocchi distinti, sorgente e distanza valide."],
            });
    }
}
