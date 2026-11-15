using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MoscoRun.Api.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoscoRun.Api.Commands;
using Microsoft.AspNetCore.Identity;
using MoscoRun.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace MoscoRun.IntegrationTests;

[Collection(HostStartupGroup.Name)]
public sealed class StartupTests(ApiFactory factory)
{
    [Theory]
    [InlineData("", null, null)]
    [InlineData("Host=db;Password=change-me", null, null)]
    [InlineData("Host=db;Username=moscorun", null, null)]
    [InlineData("not a connection string", null, null)]
    [InlineData("Host=db;Password=s3cret-real", "admin@x.it", "short")]
    [InlineData("Host=db;Password=s3cret-real", "admin@x.it", "password12345")]
    [InlineData("Host=db;Password=s3cret-real", "admin@x.it", "senza-maiuscola-9")]
    [InlineData("Host=db;Password=s3cret-real", "admin@x.it", "SENZA-MINUSCOLA-9")]
    [InlineData("Host=db;Password=s3cret-real", "admin@x.it", "Senza-Cifre-Qui")]
    public void Startup_fails_fast_on_missing_or_weak_secrets(string connection, string? email, string? password)
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:MoscoRun", connection);
            b.UseSetting("Bootstrap:AdminEmail", email);
            b.UseSetting("Bootstrap:AdminPassword", password);
        });

        var start = () => app.CreateClient();
        start.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Weak_admin_password_is_rejected_at_startup_naming_every_missing_rule()
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:MoscoRun", "Host=db;Password=s3cret-real");
            b.UseSetting("Bootstrap:AdminEmail", "admin@x.it");
            b.UseSetting("Bootstrap:AdminPassword", "tuttominuscolo");
        });

        var start = () => app.CreateClient();
        start.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("una lettera maiuscola").And.Contain("una cifra");
    }

    [Theory]
    [InlineData("Password-Forte-2026")]
    [InlineData("senza-maiuscola-9")]
    public async Task Migrate_refuses_a_configuration_the_app_would_reject_without_touching_the_database(string adminPassword)
    {
        // Host inesistente: se il comando provasse a migrare fallirebbe con un'eccezione di rete, non con il codice 4.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:MoscoRun"] = "Host=db-inesistente.invalid;Password=s3cret-real",
            ["Bootstrap:AdminEmail"] = "admin@x.it",
            ["Bootstrap:AdminPassword"] = adminPassword,
        });
        builder.AddMoscoRun();
        await using var app = builder.Build();

        (await MigrateCommand.RunAsync(app.Services)).Should().Be(MigrateCommand.InvalidConfiguration);
    }

    [Fact]
    public async Task Migrate_is_idempotent_and_does_not_recreate_admin()
    {
        (await MigrateCommand.RunAsync(factory.Services)).Should().Be(0);
        (await MigrateCommand.RunAsync(factory.Services)).Should().Be(0);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        (await users.Users.CountAsync(u => u.Email == ApiFactory.AdminEmail, TestContext.Current.CancellationToken)).Should().Be(1);
    }
}
