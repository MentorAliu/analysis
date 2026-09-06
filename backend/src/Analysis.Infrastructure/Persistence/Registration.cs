using Analysis.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Analysis.Infrastructure.Persistence;

public static class Registration
{
    public static IServiceCollection AddResearchPersistence(this IServiceCollection services)
    {
        services.AddDbContextFactory<ResearchDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.SetPostgresVersion(18, 0)));
        services.AddSingleton<IObservationStore, ObservationStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<ObservationIngestion>();
        return services;
    }
}
