using Analysis.Domain.SignalsOutcomes;

namespace Analysis.Application;

public sealed record ForwardInspectionReport(DateTimeOffset RetrievedAtUtc, ForwardRange Range,
    ForwardInspection[] Records, ForwardAggregate[] Aggregates, int UnissuedHours, object[] RunEvents);
public interface IForwardRecordReader
{
    Task<ForwardInspectionReport> InspectAsync(ForwardRange range, CancellationToken cancellationToken);
}
