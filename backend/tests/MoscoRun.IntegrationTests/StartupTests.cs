using Microsoft.AspNetCore.Hosting;
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
    public async Task Migrate_is_idempotent_and_does_not_recreate_admin()
    {
        (await MigrateCommand.RunAsync(factory.Services)).Should().Be(0);
        (await MigrateCommand.RunAsync(factory.Services)).Should().Be(0);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        (await users.Users.CountAsync(u => u.Email == ApiFactory.AdminEmail, TestContext.Current.CancellationToken)).Should().Be(1);
    }
}
