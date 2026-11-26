using System.Collections.ObjectModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class ExtendedBiomechanicsEndpoints
{
    internal static async Task<Results<Ok<ExtendedBiomechanicsReport>, ValidationProblem, NotFound>> GetAsync(
        Guid id, string source, bool protocolConfirmed, bool cadenceIsStepsPerMinute, bool balanceIsLeftPercent,
        string? chunkIds, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var ids = Parse(chunkIds);
        if (ids is null || string.IsNullOrWhiteSpace(source) || source.Length > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["protocol"] = ["Sorgente e fino a cinque ID chunk distinti e validi."],
            });
        }

        var report = await new ExtendedBiomechanicsReader(db).ReadAsync(principal.OwnerId(), id, source,
            time.GetUtcNow(), ids, protocolConfirmed, cadenceIsStepsPerMinute, balanceIsLeftPercent, ct);
        return report is not null ? TypedResults.Ok(report) : TypedResults.NotFound();
    }

    private static ReadOnlyCollection<Guid>? Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return Array.AsReadOnly(Array.Empty<Guid>());
        }

        if (raw.Length > 200)
        {
            return null;
        }

        var parts = raw.Split(',');
        var ids = new Guid[parts.Length];
        if (parts.Length > 5)
        {
            return null;
        }

        for (var index = 0; index < parts.Length; index++)
        {
            if (!Guid.TryParse(parts[index], out ids[index]) || ids[index] == Guid.Empty)
            {
                return null;
            }
        }

        return ids.Distinct().Count() == ids.Length ? Array.AsReadOnly(ids) : null;
    }
}
