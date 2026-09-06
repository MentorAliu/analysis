using System.Text.Json;
using Analysis.Application;
using Analysis.Infrastructure;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Worker;

internal static class ScoringOperation
{
    public static async Task<int> RunAsync(WebApplication app, ScoringCommand command, string runId, CancellationToken cancellationToken)
    {
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<ResearchDbContext>>().CreateDbContextAsync(cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var jobs = app.Services.GetRequiredService<ScoringJobs>();
        if (command.Score is not null)
        {
            var stored = await jobs.RunAsync(command.Score, cancellationToken);
            var notReady = stored.Bundle.Assets.Count(a => a.Score.State == "not-ready");
            Console.WriteLine(JsonSerializer.Serialize(new { mode = "m3-score", runId, batchId = stored.Id,
                model = stored.ModelId, manifestHash = stored.ManifestHash, sourceHash = stored.SourceHash,
                asOfUtc = stored.Bundle.Input.AsOfUtc, knowledgeCutoffUtc = stored.Bundle.Input.KnowledgeCutoffUtc,
                duplicate = stored.Duplicate, scores = stored.Bundle.Assets.Length, notReady,
                complete = stored.Bundle.Assets.Count(a => a.Score.State == "complete") }));
            return notReady == 0 ? 0 : 3;
        }
        var report = await jobs.ReplayAsync(command.ModelId, command.StartUtc, command.EndUtc, cancellationToken);
        Console.WriteLine(JsonSerializer.Serialize(new { mode = "m3-replay", runId, report.Batches, report.Scores, report.MissingPeriods }));
        return report.Batches == 0 ? 3 : 0;
    }
}
