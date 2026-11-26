using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutEnvironmentEndpoints
{
    internal static async Task<Results<Ok<WorkoutEnvironmentReport>, ValidationProblem, NotFound>> PostAsync(
        Guid id, WorkoutEnvironmentRequest request, ClaimsPrincipal principal, MoscoRunDbContext db,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!request.IsValid())
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Sorgente, fino a quattro chunk e parametri ambientali validi."],
            });
        }

        var report = await new WorkoutEnvironmentReader(db).ReadAsync(principal.OwnerId(), id, request, time.GetUtcNow(), ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }
}
