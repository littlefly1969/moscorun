using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Devices;

/// <param name="Id">Identificativo, incluso nel token.</param>
/// <param name="Name">Nome scelto dall'utente.</param>
/// <param name="Kind">Tipo di agente.</param>
/// <param name="CreatedAt">Registrazione.</param>
/// <param name="LastSeenAt">Ultima richiesta autenticata del dispositivo (contatto), anche se non ha portato dati.</param>
/// <param name="LastIngestedAt">Ultima pesata accettata dal dispositivo (freschezza dei dati).</param>
/// <param name="RevokedAt">Revoca del token, se avvenuta.</param>
public sealed record DeviceDto(
    Guid Id, string Name, DeviceKind Kind, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, DateTimeOffset? LastIngestedAt, DateTimeOffset? RevokedAt)
{
    public static DeviceDto From(Device d) => new(d.Id, d.Name, d.Kind, d.CreatedAt, d.LastSeenAt, d.LastIngestedAt, d.RevokedAt);
}

public sealed record RegisterDeviceRequest(
    [property: Required, MaxLength(Device.NameMaxLength)] string Name,
    [property: Required] DeviceKind? Kind);

/// <param name="Device">Dispositivo registrato.</param>
/// <param name="Token">Credenziale da configurare nell'agente. Viene mostrata solo ora.</param>
public sealed record RegisteredDevice(DeviceDto Device, string Token);

/// <summary>Identità del dispositivo autenticato con il proprio token.</summary>
/// <param name="DeviceId">Dispositivo a cui appartiene il token.</param>
/// <param name="Name">Nome scelto alla registrazione.</param>
/// <param name="Kind">Tipo di agente.</param>
/// <param name="AccountId">
/// Identificativo stabile della persona proprietaria: permette all'agente di tenere separato lo stato di
/// sincronizzazione quando viene collegato a un'altra persona o a un altro server.
/// </param>
public sealed record DeviceIdentityDto(Guid DeviceId, string Name, DeviceKind Kind, Guid AccountId);

internal static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder v1)
    {
        var devices = v1.MapGroup("/devices").RequireAuthorization(Policies.User).WithTags("Dispositivi");
        devices.MapGet("/", ListAsync).WithName("ListDevices");
        devices.MapPost("/", RegisterAsync).WithName("RegisterDevice");
        devices.MapDelete("/{id:guid}", RevokeAsync).WithName("RevokeDevice");

        v1.MapGet("/device", IdentityAsync)
            .RequireAuthorization(Policies.Ingest)
            .WithTags("Dispositivi")
            .WithName("GetDeviceIdentity")
            .WithSummary("Verifica il token del dispositivo e ne restituisce l'identità.")
            .WithDescription("Usato dagli agenti per la verifica della connessione: non modifica dati.");
        return v1;
    }

    internal static async Task<Results<Ok<DeviceIdentityDto>, NotFound>> IdentityAsync(
        ClaimsPrincipal principal, MoscoRunDbContext db, CancellationToken ct)
    {
        var deviceId = principal.DeviceId();
        var ownerId = principal.OwnerId();
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == deviceId && d.OwnerId == ownerId, ct);
        return device is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new DeviceIdentityDto(device.Id, device.Name, device.Kind, device.OwnerId));
    }

    internal static async Task<Ok<DeviceDto[]>> ListAsync(ClaimsPrincipal principal, MoscoRunDbContext db, CancellationToken ct)
    {
        var ownerId = principal.OwnerId();
        var items = await db.Devices.AsNoTracking()
            .Where(d => d.OwnerId == ownerId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return TypedResults.Ok(items.Select(DeviceDto.From).ToArray());
    }

    internal static async Task<Results<Created<RegisteredDevice>, ValidationProblem>> RegisterAsync(
        RegisterDeviceRequest request, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var deviceId = Guid.CreateVersion7(now);
        var (token, hash) = DeviceTokens.Create(deviceId);

        var result = Device.Register(deviceId, principal.OwnerId(), request.Name, request.Kind!.Value, hash, now);
        if (!result.IsSuccess)
        {
            return result.Errors.ToValidationProblem();
        }

        var device = result.Value;
        db.Devices.Add(device);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/v1/devices/{device.Id}", new RegisteredDevice(DeviceDto.From(device), token));
    }

    internal static async Task<Results<NoContent, NotFound>> RevokeAsync(
        Guid id, ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, CancellationToken ct)
    {
        var ownerId = principal.OwnerId();
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == ownerId, ct);
        if (device is null)
        {
            return TypedResults.NotFound();
        }

        device.Revoke(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
