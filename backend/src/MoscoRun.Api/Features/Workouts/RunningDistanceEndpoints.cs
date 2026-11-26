using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class RunningDistanceEndpoints
{
    internal static async Task<Results<Ok<RunningDistanceReport>, NotFound>> GetAsync(Guid id,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new RunningDistanceReader(db).ReadAsync(principal.OwnerId(), id, time.GetUtcNow(), ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }
}
