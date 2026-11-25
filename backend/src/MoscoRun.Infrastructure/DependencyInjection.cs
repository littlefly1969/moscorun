using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "MoscoRun";

    public static IServiceCollection AddMoscoRunPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        services.AddDbContext<MoscoRunDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__ef_migrations_history", "moscorun")
                .EnableRetryOnFailure(maxRetryCount: 3))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ScientificObservationStore>();
        return services;
    }
}
