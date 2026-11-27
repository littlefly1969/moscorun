using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Auth;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Science;

internal static class ScientificInventoryEndpoints
{
    public static RouteGroupBuilder MapScientificInventoryEndpoints(this RouteGroupBuilder v1)
    {
        var inventory = v1.MapGroup("/science/inventory").RequireAuthorization(Policies.User).WithTags("Acquisizione");
        inventory.MapGet("", GetAsync).WithName("GetScientificInventory")
            .WithSummary("Inventario delle revisioni scientifiche memorizzate, segnali e pesate dell'atleta.");
        inventory.MapGet("/observations", GetObservationsAsync).WithName("GetInventoryObservations")
            .WithSummary("Storico paginato delle osservazioni memorizzate, incluse revisioni e valori mancanti.");
        return v1;
    }

    private static async Task<Ok<ScientificInventory>> GetAsync(ClaimsPrincipal principal, MoscoRunDbContext db,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var result = await new ScientificInventoryReader(db).ReadAsync(principal.OwnerId(), time.GetUtcNow(), ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<InventoryObservationPage>, ValidationProblem>> GetObservationsAsync(
        string metric, string source, DateTimeOffset? asOf, DateTimeOffset? beforeTime, Guid? beforeId, int? limit,
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var now = time.GetUtcNow();
        var cutoff = asOf ?? now;
        var size = limit ?? 50;
        if (string.IsNullOrWhiteSpace(metric) || metric.Length > 200 || string.IsNullOrWhiteSpace(source) ||
            source.Length > 200 || cutoff > now || size is < 1 or > 100 ||
            beforeTime.HasValue != beforeId.HasValue || beforeTime.HasValue && !asOf.HasValue)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["inventory"] = ["Metrica, sorgente, cutoff, limite o cursore non validi."],
            });
        }

        var result = await new ScientificInventoryReader(db).ReadObservationsAsync(principal.OwnerId(), metric,
            source, cutoff, beforeTime, beforeId, size, ct);
        return TypedResults.Ok(result);
    }
}
