using Analysis.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analysis.Infrastructure.Persistence;

public sealed class ScoringModelRow
{
    public string Id { get; set; } = "";
    public string ManifestJson { get; set; } = "";
    public string ManifestHash { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class ScoringBatchRow
{
    public string Id { get; set; } = "";
    public string ModelId { get; set; } = "";
    public DateTimeOffset AsOfUtc { get; set; }
    public DateTimeOffset KnowledgeCutoffUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatingTransactionId { get; set; } = "";
    public string RecordKind { get; set; } = "";
    public string UniverseJson { get; set; } = "";
    public string InputJson { get; set; } = "";
    public string InputHash { get; set; } = "";
}
public sealed class InputObservationRow
{
    public string BatchId { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public ObservationKind Kind { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; }
    public int PeriodSeconds { get; set; }
    public string FactJson { get; set; } = "";
}
public sealed class InputConflictRow
{
    public string BatchId { get; set; } = "";
    public string ConflictId { get; set; } = "";
    public string FactJson { get; set; } = "";
}
public sealed class FeatureSnapshotRow
{
    public string Id { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string AssetId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public DateTimeOffset AsOfUtc { get; set; }
    public bool CorePriceReady { get; set; }
    public string FeatureHash { get; set; } = "";
}
public sealed class FeatureValueRow
{
    public string SnapshotId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public int FeatureId { get; set; }
    public string Key { get; set; } = "";
    public string CalculationVersion { get; set; } = "";
    public string Unit { get; set; } = "";
    public string State { get; set; } = "";
    public decimal? Value { get; set; }
    public string DetailJson { get; set; } = "";
}
public sealed class ScoreSnapshotRow
{
    public string Id { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string AssetId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public DateTimeOffset AsOfUtc { get; set; }
    public string State { get; set; } = "";
    public decimal? Composite { get; set; }
    public decimal? BullishConfidence { get; set; }
    public decimal? BearishConfidence { get; set; }
    public decimal DataQuality { get; set; }
    public decimal ContextCoverage { get; set; }
    public string ScoreJson { get; set; } = "";
    public string ScoreHash { get; set; } = "";
}
public sealed class CategoryScoreRow
{
    public string ScoreId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string Category { get; set; } = "";
    public string State { get; set; } = "";
    public decimal? Score { get; set; }
    public decimal DataQuality { get; set; }
    public int ApplicableWeight { get; set; }
    public int AvailableWeight { get; set; }
}

internal static class ScoringSchema
{
    public static readonly string[] Tables = ["ScoringModels", "ScoringBatches", "InputObservations", "InputConflicts",
        "FeatureSnapshots", "FeatureValues", "ScoreSnapshots", "CategoryScores"];

    public static void Configure(ModelBuilder model)
    {
        model.Entity<ScoringModelRow>(e =>
        {
            e.ToTable("ScoringModels"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.ManifestHash).HasMaxLength(64); e.Property(x => x.SourceHash).HasMaxLength(64);
        });
        model.Entity<ScoringBatchRow>(e =>
        {
            e.ToTable("ScoringBatches", t =>
            {
                t.HasCheckConstraint("CK_M3_batch_clock", "\"KnowledgeCutoffUtc\" >= \"AsOfUtc\" AND \"CreatedAtUtc\" >= \"KnowledgeCutoffUtc\" AND EXTRACT(EPOCH FROM \"AsOfUtc\") % 3600 = 0");
                t.HasCheckConstraint("CK_M3_record_kind", "\"RecordKind\" = 'research-reconstruction'");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(64);
            e.HasIndex(x => new { x.ModelId, x.AsOfUtc }).IsUnique();
            e.HasAlternateKey(x => new { x.Id, x.AsOfUtc, x.ModelId });
            e.Property(x => x.CreatingTransactionId).HasDefaultValueSql("pg_current_xact_id()::text");
            e.HasOne<ScoringModelRow>().WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<InputObservationRow>(e =>
        {
            e.ToTable("InputObservations");
            e.HasKey(x => new { x.BatchId, x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            Batch(e);
            e.HasOne<ObservationRow>().WithMany().HasForeignKey(x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<InputConflictRow>(e =>
        {
            e.ToTable("InputConflicts"); e.HasKey(x => new { x.BatchId, x.ConflictId }); Batch(e);
            e.HasOne<QuarantineRow>().WithMany().HasForeignKey(x => x.ConflictId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FeatureSnapshotRow>(e =>
        {
            e.ToTable("FeatureSnapshots"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(64);
            e.HasAlternateKey(x => new { x.Id, x.BatchId });
            e.HasAlternateKey(x => new { x.Id, x.BatchId, x.AssetId, x.AsOfUtc, x.ModelId });
            e.HasIndex(x => new { x.AssetId, x.AsOfUtc, x.ModelId }).IsUnique();
            e.HasOne<ScoringBatchRow>().WithMany().HasForeignKey(x => new { x.BatchId, x.AsOfUtc, x.ModelId })
                .HasPrincipalKey(x => new { x.Id, x.AsOfUtc, x.ModelId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FeatureValueRow>(e =>
        {
            e.ToTable("FeatureValues", t =>
            {
                t.HasCheckConstraint("CK_M3_feature_id", "\"FeatureId\" BETWEEN 1 AND 21");
                t.HasCheckConstraint("CK_M3_feature_state", "(\"State\" = 'available' AND \"Value\" IS NOT NULL) OR (\"State\" IN ('missing','stale','invalid','conflicted','inapplicable') AND \"Value\" IS NULL)");
                t.HasCheckConstraint("CK_M3_feature_numeric", "\"Value\" IS NULL OR (\"Value\" NOT IN ('NaN'::numeric,'Infinity'::numeric,'-Infinity'::numeric) AND scale(\"Value\") <= 18 AND abs(\"Value\") < 1e28)");
            });
            e.HasKey(x => new { x.SnapshotId, x.FeatureId });
            e.HasOne<FeatureSnapshotRow>().WithMany().HasForeignKey(x => new { x.SnapshotId, x.BatchId })
                .HasPrincipalKey(x => new { x.Id, x.BatchId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ScoreSnapshotRow>(e =>
        {
            e.ToTable("ScoreSnapshots", t =>
            {
                t.HasCheckConstraint("CK_M3_score_state", "(\"State\" IN ('complete','partial') AND \"Composite\" IS NOT NULL AND \"BullishConfidence\" IS NOT NULL AND \"BearishConfidence\" IS NOT NULL AND \"DataQuality\" >= 50) OR (\"State\" = 'not-ready' AND \"Composite\" IS NULL AND \"BullishConfidence\" IS NULL AND \"BearishConfidence\" IS NULL)");
                t.HasCheckConstraint("CK_M3_score_bounds", "(\"Composite\" IS NULL OR \"Composite\" BETWEEN -100 AND 100) AND (\"BullishConfidence\" IS NULL OR \"BullishConfidence\" BETWEEN 0 AND 100) AND (\"BearishConfidence\" IS NULL OR \"BearishConfidence\" BETWEEN 0 AND 100) AND \"DataQuality\" BETWEEN 0 AND 100 AND \"ContextCoverage\" BETWEEN 0 AND 100");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(64); e.HasAlternateKey(x => new { x.Id, x.BatchId });
            e.HasIndex(x => new { x.AssetId, x.AsOfUtc, x.ModelId }).IsUnique();
            e.HasIndex(x => new { x.ModelId, x.AsOfUtc, x.AssetId });
            e.HasOne<FeatureSnapshotRow>().WithOne().HasForeignKey<ScoreSnapshotRow>(x => new { x.SnapshotId, x.BatchId, x.AssetId, x.AsOfUtc, x.ModelId })
                .HasPrincipalKey<FeatureSnapshotRow>(x => new { x.Id, x.BatchId, x.AssetId, x.AsOfUtc, x.ModelId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CategoryScoreRow>(e =>
        {
            e.ToTable("CategoryScores", t =>
            {
                t.HasCheckConstraint("CK_M3_category", "\"Category\" IN ('price','derivatives','fundamentals','regime') AND \"State\" IN ('complete','partial','missing','inapplicable') AND \"DataQuality\" BETWEEN 0 AND 100 AND (\"Score\" IS NULL OR \"Score\" BETWEEN -100 AND 100) AND \"AvailableWeight\" BETWEEN 0 AND \"ApplicableWeight\"");
                t.HasCheckConstraint("CK_M3_category_state", "(\"State\" IN ('complete','partial') AND \"Score\" IS NOT NULL) OR (\"State\" IN ('missing','inapplicable') AND \"Score\" IS NULL)");
            });
            e.HasKey(x => new { x.ScoreId, x.Category });
            e.HasOne<ScoreSnapshotRow>().WithMany().HasForeignKey(x => new { x.ScoreId, x.BatchId })
                .HasPrincipalKey(x => new { x.Id, x.BatchId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
    private static void Batch<T>(EntityTypeBuilder<T> entity) where T : class => entity.HasOne<ScoringBatchRow>().WithMany()
        .HasForeignKey("BatchId").OnDelete(DeleteBehavior.Restrict);
}
