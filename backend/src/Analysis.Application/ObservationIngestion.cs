using Analysis.Domain;

namespace Analysis.Application;

// Opaque raw bytes are provenance, never provider DTOs or domain feature inputs.
public sealed record PayloadCapture(string RequestPath, string MappingVersion, byte[] Bytes);
public sealed record ObservationPage(PayloadCapture Payload, IReadOnlyList<Observation> Observations);
public sealed record StoreResult(int Inserted, int Duplicates);
public sealed record IngestionResult(string InstrumentId, string Status, int Inserted, int Duplicates, string? ErrorCode);

public interface IObservationAdapter
{
    string ProviderId { get; }
    Task<IReadOnlyList<ObservationPage>> ReadAsync(InstrumentRef instrument, ReadWindow window, CancellationToken cancellationToken);
}

public interface IObservationStore
{
    Task<StoreResult> SaveAsync(InstrumentRef instrument, ReadWindow window,
        IReadOnlyList<ObservationPage> pages, DateTimeOffset ingestedAtUtc, CancellationToken cancellationToken);
    Task QuarantineAsync(InstrumentRef instrument, ReadWindow window, string code,
        DateTimeOffset ingestedAtUtc, CancellationToken cancellationToken);
}

public sealed class ProviderReadException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class ObservationIngestion(IObservationStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<IngestionResult>> RunAsync(
        IReadOnlyList<InstrumentRef> instruments, IReadOnlyList<IObservationAdapter> adapters,
        ReadWindow window, CancellationToken cancellationToken)
    {
        window.Validate();
        if (window.EndUtc > clock.GetUtcNow()) throw new ArgumentException("Future windows are not permitted.");
        var results = new List<IngestionResult>();
        foreach (var instrument in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var adapter = adapters.Single(a => a.ProviderId == instrument.ProviderId);
            var now = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
            try
            {
                var pages = await adapter.ReadAsync(instrument, window, cancellationToken);
                var result = await store.SaveAsync(instrument, window, pages, now, cancellationToken);
                results.Add(new(instrument.Id, result.Inserted + result.Duplicates == 0 ? "missing" : "stored",
                    result.Inserted, result.Duplicates, null));
            }
            catch (ProviderReadException error)
            {
                await store.QuarantineAsync(instrument, window, error.Code, now, cancellationToken);
                results.Add(new(instrument.Id, "quarantined", 0, 0, error.Code));
            }
        }
        return results;
    }
}
