using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Devices;

public sealed record DeviceDto(
    Guid Id, string Name, DeviceKind Kind, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, DateTimeOffset? RevokedAt)
{
    public static DeviceDto From(Device d) => new(d.Id, d.Name, d.Kind, d.CreatedAt, d.LastSeenAt, d.RevokedAt);
}

public sealed record RegisterDeviceRequest(
    [property: Required, MaxLength(Device.NameMaxLength)] string Name,
    [property: Required] DeviceKind? Kind);

/// <param name="Device">Dispositivo registrato.</param>
/// <param name="Token">Credenziale da configurare nell'agente. Viene mostrata solo ora.</param>
public sealed record RegisteredDevice(DeviceDto Device, string Token);

internal static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder v1)
    {
        var devices = v1.MapGroup("/devices").RequireAuthorization(Policies.User).WithTags("Dispositivi");
        devices.MapGet("/", ListAsync).WithName("ListDevices");
        devices.MapPost("/", RegisterAsync).WithName("RegisterDevice");
        devices.MapDelete("/{id:guid}", RevokeAsync).WithName("RevokeDevice");
        return v1;
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
