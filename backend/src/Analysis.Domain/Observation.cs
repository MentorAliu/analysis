namespace Analysis.Domain;

// Prices and quote volumes use QuoteUnit; Volume and OI use the instrument's BaseUnit.
// Funding is a fraction at a settlement timestamp, with no assumed accrual interval.
public sealed record Observation(
    string InstrumentId, ObservationKind Kind, DateTimeOffset EventTimeUtc,
    int PeriodSeconds, string Unit, string? QuoteUnit,
    decimal? Open = null, decimal? High = null, decimal? Low = null,
    decimal? Close = null, decimal? Volume = null, decimal? QuoteVolume = null,
    decimal? Value = null)
{
    public void Validate(InstrumentRef instrument)
    {
        Utc.Require(EventTimeUtc);
        if (InstrumentId != instrument.Id || !Enum.IsDefined(Kind))
            throw new ArgumentException("Observation identity is invalid.");
        foreach (var value in new[] { Open, High, Low, Close, Volume, QuoteVolume, Value })
            if (value.HasValue) ExactDecimal.Require(value.Value);

        if (Kind == ObservationKind.Candle)
        {
            if (instrument.Kind != InstrumentKind.Spot || PeriodSeconds != 3600 ||
                EventTimeUtc.ToUnixTimeMilliseconds() % 3_600_000 != 0 ||
                Unit != instrument.BaseUnit || QuoteUnit != instrument.QuoteUnit ||
                Open is not > 0 || High is not > 0 || Low is not > 0 || Close is not > 0 ||
                Volume is not >= 0 || QuoteVolume is not >= 0 || Value is not null ||
                High < Low || Open < Low || Open > High || Close < Low || Close > High)
                throw new ArgumentException("Candle values, units or interval are invalid.");
        }
        else
        {
            if (Value is null || new[] { Open, High, Low, Close, Volume, QuoteVolume }.Any(v => v is not null) || QuoteUnit is not null)
                throw new ArgumentException("Scalar observation shape is invalid.");
            var valid = Kind switch
            {
                ObservationKind.FundingRate => instrument.Kind == InstrumentKind.LinearPerpetual &&
                    Unit == "fraction" && PeriodSeconds == 0 && Value >= -1 && Value <= 1,
                ObservationKind.OpenInterestBothSides => instrument.Kind == InstrumentKind.LinearPerpetual &&
                    Unit == instrument.BaseUnit && PeriodSeconds == 3600 && Value >= 0,
                ObservationKind.ChainTvl => instrument.Kind == InstrumentKind.Chain &&
                    Unit == "USD" && PeriodSeconds == 0 && Value >= 0,
                _ => false
            };
            if (!valid) throw new ArgumentException("Scalar observation units or values are invalid.");
        }
    }
}

public static class ExactDecimal
{
    public static decimal Parse(string text)
    {
        // Validate BEFORE decimal.Parse, which can round excess precision silently.
        if (text.Length is 0 or > 64) throw new FormatException("Invalid decimal length.");
        var unsigned = text[0] == '-' ? text[1..] : text;
        var parts = unsigned.Split('.');
        if (parts.Length > 2 || parts.Any(p => p.Length == 0 || p.Any(c => !char.IsAsciiDigit(c))))
            throw new FormatException("Expected plain base-10 decimal notation.");
        var normalized = parts[0].TrimStart('0') + (parts.Length == 2 ? parts[1].TrimEnd('0') : "");
        if (normalized.Length > 28 || (parts.Length == 2 && parts[1].TrimEnd('0').Length > 18))
            throw new FormatException("Decimal exceeds the 28-digit / 18-place contract.");
        return decimal.Parse(text, System.Globalization.NumberStyles.AllowLeadingSign |
            System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void Require(decimal value) => _ = Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
