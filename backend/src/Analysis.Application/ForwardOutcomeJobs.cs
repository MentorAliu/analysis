using Analysis.Domain.SignalsOutcomes;

namespace Analysis.Application;

public sealed record ForwardRange(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? AfterId = null);
public sealed record ForwardPage(ForwardOriginal[] Items, string? NextCursor);
public sealed record ForwardMeasurementReport(int Issuances, int AddedAssessments, string? NextCursor);
public interface IForwardOutcomeStore
{
    Task<ForwardPage> ReadPageAsync(ForwardRange range, CancellationToken cancellationToken);
    Task<int> MeasureAsync(string issuanceId, CancellationToken cancellationToken);
}
public sealed class ForwardOutcomeJobs(IForwardOutcomeStore store)
{
    public async Task<ForwardMeasurementReport> MeasureAsync(ForwardRange range, CancellationToken cancellationToken)
    {
        var page = await store.ReadPageAsync(range, cancellationToken); var added = 0;
        foreach (var issue in page.Items) added += await store.MeasureAsync(issue.Id, cancellationToken);
        return new(page.Items.Length, added, page.NextCursor);
    }
}
