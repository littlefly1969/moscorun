using Microsoft.Extensions.Options;
using MoscoRun.Api.Features.Maps;

namespace MoscoRun.Api.Hosting;

/// <summary>Intestazioni di sicurezza applicate a ogni risposta.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<MapsOptions> maps)
{
    private const string Strict =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>
    /// Elenco ufficiale dei domini di Maps JavaScript API (developers.google.com/maps/documentation/javascript/content-security-policy),
    /// variante con elenco di domini e senza 'unsafe-eval'. Si applica solo se la cartografia Google è configurata.
    /// </summary>
    private const string WithGoogleMaps =
        "default-src 'self'; " +
        "script-src 'self' https://*.googleapis.com https://*.gstatic.com *.google.com https://*.ggpht.com *.googleusercontent.com blob:; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "img-src 'self' https://*.googleapis.com https://*.gstatic.com *.google.com *.googleusercontent.com data:; " +
        "connect-src 'self' https://*.googleapis.com *.google.com https://*.gstatic.com data: blob:; " +
        "font-src 'self' https://fonts.gstatic.com; frame-src *.google.com; worker-src blob:; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private readonly string _policy = maps.Value.UsesGoogle ? WithGoogleMaps : Strict;

    // La chiave Google è limitata per referrer: serve l'origine (mai il percorso). Senza Google non esce nulla.
    private readonly string _referrer = maps.Value.UsesGoogle ? "strict-origin" : "no-referrer";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = _policy;
            headers["Referrer-Policy"] = _referrer;
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
