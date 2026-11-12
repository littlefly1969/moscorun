using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using MoscoRun.Api.Commands;
using MoscoRun.Api.Features.Devices;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Identity;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MoscoRun.IntegrationTests.ApiFactory))]

namespace MoscoRun.IntegrationTests;

/// <summary>
/// Avvia l'API reale contro un PostgreSQL vero in container (stessa versione maggiore della produzione).
/// Un solo database per l'intera esecuzione: ogni test crea i propri utenti, quindi i test sono indipendenti.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@moscorun.test";

    // Credenziali generate a ogni esecuzione (maiuscola e cifra per le regole di Identity): nessuna password nel repository.
    public static readonly string AdminPassword = $"Adm9-{Guid.NewGuid():N}";
    public static readonly string UserPassword = $"Usr9-{Guid.NewGuid():N}";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("moscorun")
        .WithUsername("moscorun")
        .WithPassword($"db-{Guid.NewGuid():N}")
        .Build();

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        (await MigrateCommand.RunAsync(Services)).Should().Be(0);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MoscoRun", ConnectionString);
        builder.UseSetting("Bootstrap:AdminEmail", AdminEmail);
        builder.UseSetting("Bootstrap:AdminPassword", AdminPassword);
        // I test creano molte sessioni dallo stesso IP; il limite reale è verificato in AuthTests.
        builder.UseSetting("Security:LoginAttemptsPerMinute", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Crea un utente nuovo e restituisce un client con la sessione già aperta.</summary>
    public async Task<(HttpClient Client, Guid UserId)> CreateUserClientAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@moscorun.test";
        Guid id;
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await users.CreateAsync(user, UserPassword)).Succeeded.Should().BeTrue();
            id = user.Id;
        }

        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = UserPassword }, TestContext.Current.CancellationToken);
        login.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        return (client, id);
    }

    /// <summary>Registra un dispositivo per l'utente e restituisce un client autenticato con il suo token.</summary>
    public async Task<(HttpClient Client, RegisteredDevice Device)> CreateDeviceClientAsync(HttpClient userClient, string name = "Bilancia")
    {
        var response = await userClient.PostAsJsonAsync("/api/v1/devices", new { name, kind = DeviceKind.ScaleAgent.ToString() }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var device = (await response.Content.ReadFromJsonAsync<RegisteredDevice>(Json.Options, TestContext.Current.CancellationToken))!;
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", device.Token);
        return (client, device);
    }
}
