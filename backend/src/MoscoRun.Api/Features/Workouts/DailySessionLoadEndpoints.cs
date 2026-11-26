using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class DailySessionLoadEndpoints
{
    internal static async Task<Results<Ok<DailySessionLoadReport>, ValidationProblem>> GetAsync(
        DateOnly last, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new DailySessionLoadReader(db).ReadAsync(principal.OwnerId(), last, time.GetUtcNow(), ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["last"] = ["Selezionare una giornata UTC conclusa, con finestra di 35 giorni valida."],
            });
    }
}
