using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using MoscoRun.Api.Auth;

namespace MoscoRun.Api.Features.Maps;

/// <param name="Provider">Fornitore della cartografia: <c>google</c> o <c>none</c>.</param>
/// <param name="GoogleApiKey">Chiave per il browser, presente solo con Google.</param>
public sealed record MapsConfigDto(string Provider, string? GoogleApiKey);

internal static class MapEndpoints
{
    public static RouteGroupBuilder MapMapEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/maps/config", GetConfig)
            .RequireAuthorization(Policies.User)
            .WithTags("Mappe")
            .WithName("GetMapsConfig")
            .WithSummary("Configurazione della cartografia per il cruscotto.");
        return v1;
    }

    internal static Ok<MapsConfigDto> GetConfig(IOptions<MapsOptions> options)
    {
        var maps = options.Value;
        return TypedResults.Ok(new MapsConfigDto(maps.EffectiveProvider, maps.UsesGoogle ? maps.GoogleApiKey!.Trim() : null));
    }
}
