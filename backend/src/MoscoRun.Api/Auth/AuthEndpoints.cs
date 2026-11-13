using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using MoscoRun.Api.Hosting;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Api.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MaxLength(256)] string Password);

public sealed record CurrentUser(Guid Id, string Email, string? DisplayName);

internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Autenticazione");

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ApplicationSetup.LoginRateLimitPolicy)
            .WithName("Login")
            .WithSummary("Accede al cruscotto e imposta il cookie di sessione.");

        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(Policies.User)
            .WithName("Logout");

        auth.MapGet("/me", MeAsync)
            .RequireAuthorization(Policies.User)
            .WithName("GetCurrentUser");

        return api;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LoginAsync(
        LoginRequest request, SignInManager<AppUser> signIn, HttpContext http, ILoggerFactory loggers)
    {
        var result = await signIn.PasswordSignInAsync(request.Email, request.Password, isPersistent: true, lockoutOnFailure: true);
        var log = loggers.CreateLogger(AuthLog.Category);
        var clientIp = http.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";
        if (result.Succeeded)
        {
            AuthLog.LoginSucceeded(log, clientIp);
            return TypedResults.NoContent();
        }

        // Nei log l'IP reale (dopo i proxy fidati) ma non l'email: niente dati personali né elenco di account.
        if (result.IsLockedOut)
        {
            AuthLog.LockedOut(log, clientIp);
        }
        else
        {
            AuthLog.LoginFailed(log, clientIp);
        }

        // Messaggio volutamente generico: non rivela se l'utente esiste.
        return result.IsLockedOut
            ? TypedResults.Problem("Troppi tentativi: account bloccato temporaneamente.", statusCode: StatusCodes.Status423Locked)
            : TypedResults.Problem("Credenziali non valide.", statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult>> MeAsync(
        System.Security.Claims.ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new CurrentUser(user.Id, user.Email ?? string.Empty, user.DisplayName));
    }
}
