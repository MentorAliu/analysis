using System.Globalization;
using System.Text.RegularExpressions;
using Analysis.Application;

namespace Analysis.Api.Rankings;

public static class RankingTransport
{
    public static string Timestamp(DateTimeOffset value)
    {
        Analysis.Domain.Utc.Require(value);
        return value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
    public static string Decimal(decimal value, bool signed = false)
    {
        Require(value >= (signed ? -100m : 0m) && value <= 100m && decimal.Round(value, 6) == value);
        return (value == 0m ? 0m : value).ToString("F6", CultureInfo.InvariantCulture);
    }
    private static string Hash(string value)
    { Require(Regex.IsMatch(value, Wire.Hash, RegexOptions.CultureInvariant)); return value; }
    public static RankingsResponse Map(RankingsRequest request, RankingsReadBatch batch, DateTimeOffset now)
    {
        var manifest = batch.Manifest;
        Require(batch.AsOfUtc <= now && batch.AsOfUtc <= batch.KnowledgeCutoffUtc && batch.KnowledgeCutoffUtc <= batch.CreatedAtUtc);
        var rank = 0;
        var items = batch.Items.OrderBy(i => i.Score.Composite is null)
            .ThenByDescending(i => i.Score.Composite).ThenBy(i => i.Asset.Id, StringComparer.Ordinal).Select(i =>
            {
                var s = i.Score;
                var state = s.State switch { "complete" => RankingState.complete, "partial" => RankingState.partial,
                    "not-ready" => RankingState.NotReady, _ => throw new RankingsReadException("rankings-integrity-failure") };
                var ready = state != RankingState.NotReady;
                Require(ready == s.Composite.HasValue && ready == s.BullishConfidence.HasValue && ready == s.BearishConfidence.HasValue);
                Require(!ready || i.CorePriceReady && s.DataQuality >= manifest.History.MinimumQuality);
                Require(state != RankingState.complete || s.DataQuality == 100m && s.ContextCoverage == 100m);
                Require(s.Categories.Select(c => c.Category).SequenceEqual(new[] { "price", "derivatives", "fundamentals", "regime" }));
                var categories = s.Categories.Select(c =>
                {
                    var cs = c.State switch { "complete" => CategoryState.complete, "partial" => CategoryState.partial,
                        "missing" => CategoryState.missing, "inapplicable" => CategoryState.inapplicable,
                        _ => throw new RankingsReadException("rankings-integrity-failure") };
                    Require(c.ApplicableWeight >= 0 && c.AvailableWeight >= 0 && c.AvailableWeight <= c.ApplicableWeight &&
                        ((cs is CategoryState.complete or CategoryState.partial) == c.Score.HasValue));
                    Require(cs switch { CategoryState.inapplicable => c.ApplicableWeight == 0 && c.DataQuality == 0,
                        CategoryState.missing => c.ApplicableWeight > 0 && c.AvailableWeight == 0 && c.DataQuality == 0,
                        CategoryState.complete => c.ApplicableWeight > 0 && c.AvailableWeight == c.ApplicableWeight && c.DataQuality == 100,
                        _ => c.AvailableWeight > 0 && c.AvailableWeight < c.ApplicableWeight });
                    return new RankingCategory(Enum.Parse<CategoryName>(c.Category), cs, c.Score.HasValue ? Decimal(c.Score.Value, true) : null,
                        Decimal(c.DataQuality), c.ApplicableWeight, c.AvailableWeight);
                }).ToArray();
                Require(categories.Sum(c => (long)c.ApplicableWeightNumerator) == manifest.Numeric.WeightDenominator);
                var f = i.FeatureStateCounts;
                Require(new[] { f.Available, f.Missing, f.Stale, f.Invalid, f.Conflicted, f.Inapplicable }.All(n => n >= 0) &&
                    f.Available + f.Missing + f.Stale + f.Invalid + f.Conflicted + f.Inapplicable == 21);
                return new RankingItem(i.Asset.Id, i.Asset.Symbol, i.Asset.Name, ready ? ++rank : null,
                    Hash(i.ScoreSnapshotId), Hash(i.FeatureSnapshotId), Hash(i.ScoreHash), Hash(i.FeatureHash), state,
                    s.Composite.HasValue ? Decimal(s.Composite.Value, true) : null,
                    s.BullishConfidence.HasValue ? Decimal(s.BullishConfidence.Value) : null,
                    s.BearishConfidence.HasValue ? Decimal(s.BearishConfidence.Value) : null,
                    new(Decimal(s.DataQuality), Decimal(s.ContextCoverage), s.ProviderAgreement, i.CorePriceReady,
                        new(f.Available, f.Missing, f.Stale, f.Invalid, f.Conflicted, f.Inapplicable)), categories);
            }).ToArray();
        Require(items.Select(i => i.AssetId).Order(StringComparer.Ordinal).SequenceEqual(batch.UniverseAssetIds.Order(StringComparer.Ordinal)));
        return new(request.AsOfUtc.HasValue ? RankingSelection.exact : RankingSelection.latest,
            request.AsOfUtc?.ToString("yyyy-MM-dd'T'HH:00:00'Z'", CultureInfo.InvariantCulture), Timestamp(now),
            (now.Ticks - batch.AsOfUtc.Ticks) / TimeSpan.TicksPerSecond, "score-points",
            new(Hash(batch.Id), Timestamp(batch.AsOfUtc), Timestamp(batch.KnowledgeCutoffUtc), Timestamp(batch.CreatedAtUtc),
                batch.RecordKind, Hash(batch.InputHash), batch.UniverseAssetIds,
                new(manifest.ModelId, Hash(batch.ManifestHash), Hash(batch.CalculatorSourceHash), manifest.FeatureVersion,
                    manifest.ScorerVersion, manifest.Numeric.Version, manifest.Status, manifest.Numeric.WeightDenominator)), items);
    }
    private static void Require(bool value)
    { if (!value) throw new RankingsReadException("rankings-integrity-failure"); }
}
