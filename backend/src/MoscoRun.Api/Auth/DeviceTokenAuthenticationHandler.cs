using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Auth;

/// <summary>Autentica gli agenti tramite <c>Authorization: Bearer mr1_...</c>.</summary>
internal sealed partial class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    MoscoRunDbContext db,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        if (!DeviceTokens.TryParse(header["Bearer ".Length..].Trim(), out var deviceId, out var secret))
        {
            return AuthenticateResult.Fail("Token di dispositivo malformato.");
        }

        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId, Context.RequestAborted);
        if (device is null || !device.IsActive || !DeviceTokens.Matches(secret, device.TokenHash))
        {
            LogRejected(Logger, deviceId);
            return AuthenticateResult.Fail("Token di dispositivo non valido o revocato.");
        }

        if (device.Touch(time.GetUtcNow()))
        {
            await db.SaveChangesAsync(Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, device.OwnerId.ToString()),
                new Claim(MoscoRunClaims.DeviceId, device.Id.ToString()),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token rifiutato per il dispositivo {DeviceId}")]
    private static partial void LogRejected(ILogger logger, Guid deviceId);
}
