namespace Analysis.Domain;

public sealed record Asset(string Id, string Symbol, string Name);

public enum InstrumentKind { Spot, LinearPerpetual, Chain }

public sealed record InstrumentRef(
    string Id, string AssetId, string ProviderId, string NativeSymbol,
    InstrumentKind Kind, string BaseUnit, string? QuoteUnit, string? SettlementUnit);

public enum ObservationKind { Candle, FundingRate, OpenInterestBothSides, ChainTvl }

public sealed record ReadWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public void Validate()
    {
        Utc.Require(StartUtc);
        Utc.Require(EndUtc);
        if (EndUtc <= StartUtc || EndUtc - StartUtc > TimeSpan.FromDays(30))
            throw new ArgumentException("A read window must be positive and at most 30 days.");
    }
}

public static class Utc
{
    public static void Require(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMillisecond != 0)
            throw new ArgumentException("Timestamps must use UTC and exact millisecond precision.");
    }
}
