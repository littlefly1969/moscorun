using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class SleepDiaryEndpoints
{
    internal static async Task<Results<Ok<SleepDiaryReport>, ValidationProblem>> GetAsync(DateOnly last, int days,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new SleepDiaryReader(db).ReadAsync(principal.OwnerId(), last, days, time.GetUtcNow(), ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["window"] = ["Selezionare da 7 a 90 notti concluse nel calendario UTC."],
            });
    }
}
