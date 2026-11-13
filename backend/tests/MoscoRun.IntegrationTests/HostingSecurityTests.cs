using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using MoscoRun.Api.Commands;
using MoscoRun.Infrastructure.Persistence;
using Npgsql;

namespace MoscoRun.IntegrationTests;

[Collection(HostStartupGroup.Name)]
public sealed class HostingSecurityTests(ApiFactory factory)
{
    private const string ProxyIp = "172.30.85.1";
    private static readonly string Certificate = GenerateCertificate();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Data_protection_keys_are_encrypted_at_rest_with_the_configured_certificate()
    {
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("DataProtection:CertificateBase64", Certificate));
        var keys = app.Services.GetRequiredService<IKeyManager>();
        var key = keys.CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        var xml = await db.DataProtectionKeys.AsNoTracking()
            .Where(k => k.FriendlyName == $"key-{key.KeyId:D}")
            .Select(k => k.Xml)
            .SingleAsync(Ct);

        xml.Should().Contain("encryptedSecret").And.NotContain("<masterKey");

        // La chiave cifrata resta utilizzabile: un login con quella configurazione funziona.
        var (email, _) = await factory.CreateUserAsync();
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.UserPassword }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("non-base64!")]
    [InlineData("AAAA")]
    public void Production_refuses_to_start_without_a_valid_data_protection_certificate(string? certificate)
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:MoscoRun", factory.ConnectionString);
            b.UseSetting("DataProtection:CertificateBase64", certificate);
        });

        var start = () => app.CreateClient();
        start.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("DataProtection__CertificateBase64");
    }

    [Theory]
    [InlineData(ProxyIp, true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.0.0.7", false)]
    [InlineData("192.168.1.20", false)]
    public async Task Forwarded_headers_are_accepted_only_from_configured_proxies(string peer, bool trusted)
    {
        using var app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Security:TrustedProxies:0", ProxyIp);
            b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new PeerAddressFilter(peer)));
        });
        var (email, _) = await factory.CreateUserAsync();
        var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = ApiFactory.UserPassword }),
        };
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");

        var response = await client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".MoscoRun.Session", StringComparison.Ordinal));
        // Il cookie è "secure" solo se lo schema HTTPS dichiarato dal proxy è stato accettato.
        cookie.Contains("secure", StringComparison.OrdinalIgnoreCase).Should().Be(trusted);
    }

    [Theory]
    [InlineData(ProxyIp, "203.0.113.9")]
    [InlineData("192.168.1.20", "192.168.1.20")]
    public async Task Security_log_records_the_real_client_address_without_the_email(string peer, string expectedClientIp)
    {
        var logs = new FakeLogCollector();
        using var app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Security:TrustedProxies:0", ProxyIp);
            b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new PeerAddressFilter(peer)));
            b.ConfigureLogging(l => l.AddProvider(new FakeLoggerProvider(logs)));
        });
        var (email, _) = await factory.CreateUserAsync();
        var client = app.CreateClient();

        foreach (var password in new[] { "Sbagliata-1", ApiFactory.UserPassword })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email, password }),
            };
            request.Headers.Add("X-Forwarded-For", "203.0.113.9");
            await client.SendAsync(request, Ct);
        }

        var security = logs.GetSnapshot().Where(r => string.Equals(r.Category, "MoscoRun.Security", StringComparison.Ordinal)).ToList();
        security.Select(r => (r.Id.Id, r.Message)).Should().Equal(
            (1002, $"Accesso non riuscito da {expectedClientIp}"),
            (1001, $"Accesso riuscito da {expectedClientIp}"));
        security.Should().NotContain(r => r.Message.Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Readiness_fails_when_the_database_is_unreachable_while_liveness_stays_up()
    {
        using var app = factory.WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:MoscoRun", "Host=127.0.0.1;Port=1;Database=moscorun;Username=moscorun;Password=unreachable-db;Timeout=2"));
        var client = app.CreateClient();

        (await client.GetAsync("/health/live", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", Ct)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Readiness_fails_when_the_schema_does_not_match_this_version()
    {
        // Database raggiungibile ma senza le migrazioni di questa versione: connessione ok, schema no.
        var database = $"vuoto_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(factory.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
            await create.ExecuteNonQueryAsync(Ct);
        }

        var empty = new NpgsqlConnectionStringBuilder(factory.ConnectionString) { Database = database }.ConnectionString;
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:MoscoRun", empty));
        var client = app.CreateClient();

        (await client.GetAsync("/health/live", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", Ct)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static string GenerateCertificate()
    {
        using var writer = new StringWriter();
        DataProtectionCertificateCommand.Run(writer).Should().Be(0);
        return writer.ToString().Trim();
    }

    /// <summary>Simula l'indirizzo del peer TCP, che TestServer non imposta.</summary>
    private sealed class PeerAddressFilter(string peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
