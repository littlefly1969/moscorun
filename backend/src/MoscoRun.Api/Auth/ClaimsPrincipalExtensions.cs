using System.Globalization;
using System.Security.Claims;

namespace MoscoRun.Api.Auth;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>Ambito di proprietà dei dati, ricavato solo dalle credenziali (mai dal client).</summary>
    public static Guid OwnerId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Utente senza identificativo."), CultureInfo.InvariantCulture);

    public static Guid DeviceId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(MoscoRunClaims.DeviceId)
            ?? throw new InvalidOperationException("Principale senza dispositivo."), CultureInfo.InvariantCulture);
}
