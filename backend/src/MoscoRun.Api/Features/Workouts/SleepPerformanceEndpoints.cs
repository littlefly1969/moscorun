using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class SleepPerformanceEndpoints
{
    internal static async Task<Results<Ok<SleepPerformanceReport>, ValidationProblem>> GetAsync(
        string source, DateOnly last, int days, int lag, double distanceMeters,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new SleepPerformanceReader(db).ReadAsync(principal.OwnerId(), source,
            last, days, lag, distanceMeters, time.GetUtcNow(), ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Finestra 7–90 giorni UTC conclusi, lag 1–7 giorni, fonte e distanza valide."],
            });
    }
}
