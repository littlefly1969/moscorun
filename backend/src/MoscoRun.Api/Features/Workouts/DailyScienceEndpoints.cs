using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class DailyScienceEndpoints
{
    internal static async Task<Results<Ok<DailyMetricReport>, ValidationProblem>> GetAsync(
        string metric, string source, DateOnly day, int baselineDays, DateTimeOffset? asOf,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var cutoff = asOf ?? now;
        http.Response.Headers.CacheControl = "private, no-store";
        if (cutoff > now)
        {
            return Invalid();
        }

        var report = await new ScientificDailyMetricReader(db).ReadAsync(principal.OwnerId(), metric,
            source, day, baselineDays, cutoff, ct);
        return report is not null ? TypedResults.Ok(report) : Invalid();
    }

    private static ValidationProblem Invalid() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["protocol"] = ["Metrica, sorgente, giorno UTC concluso, cutoff o finestra non validi; ridurre la finestra se troppo densa."],
        });
}
