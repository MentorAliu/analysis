using Analysis.Domain;

namespace Analysis.Application;

public sealed record ForwardCollectionWork(InstrumentRef Instrument, ReadWindow Window);
public sealed record ForwardStoreResult(int Inserted, int Duplicates, int Conflicts);
public sealed record ForwardCollectionResult(string InstrumentId, ReadWindow Window, string Status,
    int Inserted, int Duplicates, int Conflicts, string? ErrorCode);
public interface IForwardCollectionStore
{
    Task<ForwardCollectionWork[]> PlanInputsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken);
    Task<ForwardCollectionWork[]> PlanOutcomesAsync(ReadWindow window, CancellationToken cancellationToken);
    Task<ForwardStoreResult> SaveAsync(ForwardCollectionWork work, IReadOnlyList<ObservationPage> pages, CancellationToken cancellationToken);
    Task QuarantineAsync(ForwardCollectionWork work, string code, CancellationToken cancellationToken);
}
public sealed class ForwardCollection(IForwardCollectionStore store)
{
    public async Task<ForwardCollectionResult[]> CollectAsync(ForwardCollectionWork[] work, IObservationAdapter[] adapters, CancellationToken cancellationToken)
    {
        var results = new List<ForwardCollectionResult>();
        foreach (var item in work)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var pages = await adapters.Single(a => a.ProviderId == item.Instrument.ProviderId).ReadAsync(item.Instrument, item.Window, cancellationToken);
                var saved = await store.SaveAsync(item, pages, cancellationToken);
                results.Add(new(item.Instrument.Id, item.Window, saved.Conflicts > 0 ? "conflicted" : saved.Inserted + saved.Duplicates == 0 ? "missing" : "stored",
                    saved.Inserted, saved.Duplicates, saved.Conflicts, null));
            }
            catch (ProviderReadException error)
            {
                await store.QuarantineAsync(item, error.Code, cancellationToken);
                results.Add(new(item.Instrument.Id, item.Window, "quarantined", 0, 0, 0, error.Code));
            }
        }
        return results.ToArray();
    }
}
