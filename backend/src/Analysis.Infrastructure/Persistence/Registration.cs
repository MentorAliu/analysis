using Analysis.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Analysis.Infrastructure.Persistence;

public static class Registration
{
    // API registration intentionally excludes ingestion/scoring writers and jobs.
    public static IServiceCollection AddRankingsReads(this IServiceCollection services)
    {
        services.AddDbContextFactory<ResearchDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.SetPostgresVersion(18, 0)));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRankingsReader, RankingsReader>();
        return services;
    }

    public static IServiceCollection AddResearchPersistence(this IServiceCollection services)
    {
        services.AddDbContextFactory<ResearchDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.SetPostgresVersion(18, 0)));
        services.AddSingleton<IObservationStore, ObservationStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<ObservationIngestion>();
        services.AddSingleton<ScoringStore>();
        services.AddSingleton<IScoringInputReader>(provider => provider.GetRequiredService<ScoringStore>());
        services.AddSingleton<IScoringStore>(provider => provider.GetRequiredService<ScoringStore>());
        services.AddTransient<ScoringJobs>();
        return services;
    }
}
