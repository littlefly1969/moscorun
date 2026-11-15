using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace MoscoRun.IntegrationTests;

/// <summary>
/// Le richieste ricorrenti (readiness ogni 30 s dal HEALTHCHECK di Docker, elenco pesate) non devono riempire
/// i log del server di avvisi di EF Core.
/// </summary>
[Collection(HostStartupGroup.Name)]
public sealed class LogHygieneTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Readiness_and_weighings_list_log_no_entity_framework_warnings()
    {
        var logs = new FakeLogCollector();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(new FakeLoggerProvider(logs))));
        var (email, _) = await factory.CreateUserAsync();
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.UserPassword }, Ct)).EnsureSuccessStatusCode();

        (await client.GetAsync("/health/ready", Ct)).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/v1/weighings", Ct)).EnsureSuccessStatusCode();

        logs.GetSnapshot()
            // Solo gli avvisi sulle query: altri test della suite possono alterare il database condiviso in parallelo.
            .Where(r => r.Level >= LogLevel.Warning && string.Equals(r.Category, "Microsoft.EntityFrameworkCore.Query", StringComparison.Ordinal))
            .Select(r => r.Message)
            .Should().BeEmpty();
    }
}
