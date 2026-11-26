using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class PersonalResearchEndpoints
{
    internal static async Task<Results<Ok<PersonalResearchReport>, ValidationProblem>> GetAsync(
        string source, DateOnly from, DateOnly split, DateOnly until, double distanceMeters, double penalty,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!PersonalResearchEngine.ValidPenalty(penalty))
        {
            return Invalid();
        }

        var owner = principal.OwnerId();
        var input = await new PersonalAdaptationReader(db).ReadAsync(owner, source, from, split, until,
            time.GetUtcNow(), distanceMeters, ct);
        var report = input is null ? null : PersonalResearchEngine.Build(owner, input, penalty);
        return report is not null ? TypedResults.Ok(report) : Invalid();
    }

    private static ValidationProblem Invalid() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["protocol"] = ["Finestra valida, almeno due giorni holdout, distanza e penalità L2 positive nel range."],
        });
}
