using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class RrChunkEndpoints
{
    internal static async Task<Results<Ok<RrChunkReport>, ValidationProblem, NotFound>> GetAsync(
        string source, DateOnly day, string context, bool screened, bool timestampsAreBeatEnds,
        Guid? chunkId, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var report = await new RrChunkReader(db).ReadAsync(principal.OwnerId(), source, day, time.GetUtcNow(),
            chunkId, context, screened, timestampsAreBeatEnds, ct);
        if (report is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Sorgente, data UTC e contesto RR validi."],
            });
        }

        return chunkId.HasValue && report.Selected is null ? TypedResults.NotFound() : TypedResults.Ok(report);
    }
}
