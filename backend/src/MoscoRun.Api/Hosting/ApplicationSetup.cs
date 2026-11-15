using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using MoscoRun.Api.Auth;
using MoscoRun.Api.Features.Dashboard;
using MoscoRun.Api.Features.Devices;
using MoscoRun.Api.Features.Maps;
using MoscoRun.Api.Features.System;
using MoscoRun.Api.Features.Weighings;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Infrastructure;
using MoscoRun.Infrastructure.Identity;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Hosting;

public static class ApplicationSetup
{
    public const string LoginRateLimitPolicy = "login";

    public static WebApplicationBuilder AddMoscoRun(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.AddSingleton(TimeProvider.System);
        if (!AppInfo.IsGeneratingOpenApiDocument)
        {
            StartupSettingsValidation.Register(services, builder.Configuration, builder.Environment);
        }

        services.AddProblemDetails();

        // Il telefono comprime gli allenamenti (gzip); il limite di dimensione vale sul corpo decompresso.
        services.AddRequestDecompression();
        services.AddValidation();
        services.ConfigureHttpJsonOptions(o =>
        {
            // Numeri solo come numeri JSON: contratto preciso per client e agenti.
            o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddOpenApi(o => o.AddDocumentTransformer<SecuritySchemeTransformer>());

        services.AddMoscoRunPersistence(builder.Configuration);

        services.AddOptions<MapsOptions>().BindConfiguration(MapsOptions.Section).ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<MapsOptions>, MapsOptionsValidation>();
        services.AddOptions<PrivacyOptions>().BindConfiguration(PrivacyOptions.Section).ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<PrivacyOptions>, PrivacyOptionsValidation>();

        AddIdentityAndSecurity(services, builder.Configuration);

        services.AddHealthChecks()
            .AddDbContextCheck<MoscoRunDbContext>("database", tags: ["ready"])
            .AddCheck<SchemaHealthCheck>("schema", tags: ["ready"]);

        var loginPermits = builder.Configuration.GetValue("Security:LoginAttemptsPerMinute", 10);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (context, _) =>
            {
                var http = context.HttpContext;
                AuthLog.RateLimited(
                    http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(AuthLog.Category),
                    http.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto");
                return ValueTask.CompletedTask;
            };
            o.AddPolicy(LoginRateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromMinutes(1) }));
        });

        ConfigureForwardedHeaders(services, builder.Configuration);

        return builder;
    }

    /// <summary>
    /// Le intestazioni X-Forwarded-* sono accettate solo dai proxy indicati: per impostazione predefinita il solo loopback.
    /// In produzione nginx sull'host raggiunge il container tramite il gateway della rete Docker del progetto
    /// (<c>Security:TrustedProxies</c>, impostato da compose.yaml). Nessuna intera rete privata è considerata fidata.
    /// </summary>
    private static void ConfigureForwardedHeaders(IServiceCollection services, ConfigurationManager configuration)
    {
        var proxies = configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            o.KnownProxies.Add(IPAddress.Loopback);
            o.KnownProxies.Add(IPAddress.IPv6Loopback);
            foreach (var proxy in proxies.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                o.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));
            }
        });
    }

    private static void AddIdentityAndSecurity(IServiceCollection services, ConfigurationManager configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("MoscoRun");
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString(DependencyInjection.ConnectionStringName)))
        {
            // Chiavi nel database: i cookie di sessione sopravvivono ad aggiornamenti e riavvii del container.
            // Senza connessione (es. generazione del contratto OpenAPI in build) restano in memoria.
            dataProtection.PersistKeysToDbContext<MoscoRunDbContext>();
        }

        // Chiavi cifrate a riposo con il certificato in .env: un dump del database (o un backup sottratto)
        // non basta a falsificare i cookie di sessione. Obbligatorio in produzione (StartupSettingsValidation).
        if (DataProtectionCertificate.TryLoad(configuration[DataProtectionCertificate.ConfigurationKey], out var certificate, out _))
        {
            dataProtection.ProtectKeysWithCertificate(certificate!).UnprotectKeysWithAnyCertificate(certificate!);
        }

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 12;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<MoscoRunDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddMoscoRunAuthentication();
    }

    public static WebApplication UseMoscoRun(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseRequestDecompression();

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // I file in /assets hanno l'hash nel nome: possono essere messi in cache per sempre.
                var immutable = ctx.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal);
                ctx.Context.Response.Headers.CacheControl = immutable ? "public, max-age=31536000, immutable" : "no-cache";
            },
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapOpenApi("/api/openapi/{documentName}.json");
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

        var api = app.MapGroup("/api");
        api.MapAuthEndpoints();
        api.MapSystemEndpoints();

        var v1 = api.MapGroup("/v1");
        v1.MapWeighingEndpoints();
        v1.MapWorkoutEndpoints();
        v1.MapDeviceEndpoints();
        v1.MapDashboardEndpoints();
        v1.MapMapEndpoints();

        // Le rotte /api inesistenti restano 404; tutto il resto è gestito dal router React.
        api.Map("{**path}", () => Results.NotFound()).ExcludeFromDescription();
        app.MapFallbackToFile("index.html");

        return app;
    }
}
