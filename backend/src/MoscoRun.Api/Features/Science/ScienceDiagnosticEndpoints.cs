using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Science;

internal static class ScienceDiagnosticEndpoints
{
    public static RouteGroupBuilder MapScienceDiagnosticEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/ingest/science-diagnostic", SaveAsync)
            .RequireAuthorization(Policies.ScientificIngest)
            .WithTags("Acquisizione")
            .WithName("SaveScienceDiagnostic")
            .WithSummary("Registra l'ultima diagnostica di acquisizione del telefono senza valori biometrici.");
        return v1;
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> SaveAsync(
        ScienceDiagnosticReport report, ClaimsPrincipal principal, MoscoRunDbContext db,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!ScienceDiagnosticStore.IsValid(report, time.GetUtcNow()))
        {
            return Invalid();
        }

        try
        {
            var accepted = await new ScienceDiagnosticStore(db, time).SaveAsync(principal.OwnerId(), principal.DeviceId(), report, ct);
            return accepted ? TypedResults.NoContent() : Conflict();
        }
        catch (ArgumentException)
        {
            return Invalid();
        }
        catch (InvalidOperationException)
        {
            return Conflict();
        }
    }

    private static ValidationProblem Invalid() => TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["report"] = ["Diagnostica, contatori o contesto non validi."],
    });

    private static ProblemHttpResult Conflict() => TypedResults.Problem(
        statusCode: StatusCodes.Status409Conflict, title: "Diagnostica obsoleta, conflittuale o acquisizione sospesa.");
}
