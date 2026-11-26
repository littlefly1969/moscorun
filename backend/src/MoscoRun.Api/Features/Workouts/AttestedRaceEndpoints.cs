using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class AttestedRaceEndpoints
{
    internal static async Task<Results<Ok<AttestedRaceReport>, ValidationProblem>> GetAsync(
        string source, DateOnly from, DateOnly split, DateOnly until, double targetMeters,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new AttestedRaceReader(db).ReadAsync(principal.OwnerId(), source,
            At(from), At(split), At(until), time.GetUtcNow(), targetMeters, ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Finestra UTC fino a 366 giorni, sorgente e distanza valide; prove non sovrapposte."],
            });
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
