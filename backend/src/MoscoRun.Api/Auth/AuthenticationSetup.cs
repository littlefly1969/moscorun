using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace MoscoRun.Api.Auth;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddMoscoRunAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies(cookies => cookies.ApplicationCookie?.Configure(o =>
            {
                o.Cookie.Name = ".MoscoRun.Session";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(14);
                o.SlidingExpiration = true;
                // API JSON: nessun redirect a pagine di login, solo codici di stato.
                o.Events.OnRedirectToLogin = ctx => SetStatus(ctx, StatusCodes.Status401Unauthorized);
                o.Events.OnRedirectToAccessDenied = ctx => SetStatus(ctx, StatusCodes.Status403Forbidden);
            }));

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(DeviceTokenAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.User, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser())
            .AddPolicy(Policies.Ingest, p => p
                .AddAuthenticationSchemes(DeviceTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(MoscoRunClaims.DeviceId));

        return services;
    }

    private static Task SetStatus(Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> ctx, int status)
    {
        ctx.Response.StatusCode = status;
        return Task.CompletedTask;
    }
}
