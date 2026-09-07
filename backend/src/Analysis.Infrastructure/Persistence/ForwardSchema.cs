using Analysis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

public sealed class ForwardMethodologyRow
{
    public string Id { get; set; } = "";
    public string ManifestJson { get; set; } = "";
    public string ManifestHash { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class ForwardIssuanceRow
{
    public string Id { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string MethodologyId { get; set; } = "";
    public DateTimeOffset AsOfUtc { get; set; }
    public DateTimeOffset KnowledgeCutoffUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ReferenceBoundaryUtc { get; set; }
    public string OriginalJson { get; set; } = "";
    public string OriginalHash { get; set; } = "";
    public string CreatingTransactionId { get; set; } = "";
}
public sealed class ForwardIssuanceItemRow
{
    public string IssuanceId { get; set; } = "";
    public string AssetId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string ScoreSnapshotId { get; set; } = "";
    public int? Rank { get; set; }
    public string ItemJson { get; set; } = "";
}
public sealed class ForwardOutcomeTargetRow
{
    public string IssuanceId { get; set; } = "";
    public string AssetId { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public int HorizonHours { get; set; }
    public DateTimeOffset ReferenceBoundaryUtc { get; set; }
    public DateTimeOffset MaturityUtc { get; set; }
}
public sealed class ForwardOutcomeAssessmentRow
{
    public string Id { get; set; } = "";
    public string IssuanceId { get; set; } = "";
    public string AssetId { get; set; } = "";
    public int HorizonHours { get; set; }
    public int Sequence { get; set; }
    public DateTimeOffset AssessedAtUtc { get; set; }
    public DateTimeOffset KnowledgeCutoffUtc { get; set; }
    public string State { get; set; } = "";
    public decimal? ReferencePrice { get; set; }
    public decimal? TerminalPrice { get; set; }
    public decimal? Return { get; set; }
    public string? OriginalCompleteAssessmentId { get; set; }
    public string ResultJson { get; set; } = "";
    public string EvidenceHash { get; set; } = "";
    public string CreatingTransactionId { get; set; } = "";
}
public sealed class ForwardOutcomeInputRow
{
    public string AssessmentId { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public ObservationKind Kind { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; }
    public int PeriodSeconds { get; set; }
    public string PayloadId { get; set; } = "";
    public string FactJson { get; set; } = "";
}
public sealed class ForwardObservationConflictRow
{
    public string Id { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public ObservationKind Kind { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; }
    public int PeriodSeconds { get; set; }
    public string QuarantineId { get; set; } = "";
    public string CandidatePayloadId { get; set; } = "";
    public string OriginalFactJson { get; set; } = "";
    public string CandidateJson { get; set; } = "";
    public DateTimeOffset DetectedAtUtc { get; set; }
}
public sealed class ForwardRunEventRow
{
    public string Id { get; set; } = "";
    public string RunId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string State { get; set; } = "";
    public DateTimeOffset AtUtc { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public string DetailJson { get; set; } = "";
}

internal static class ForwardSchema
{
    public static readonly string[] Tables = ["ForwardMethodologies", "ForwardIssuances", "ForwardIssuanceItems",
        "ForwardOutcomeTargets", "ForwardOutcomeAssessments", "ForwardOutcomeInputs", "ForwardObservationConflicts", "ForwardRunEvents"];
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ForwardMethodologyRow>(e => { e.ToTable("ForwardMethodologies"); e.HasKey(x => x.Id); });
        model.Entity<ForwardIssuanceRow>(e =>
        {
            e.ToTable("ForwardIssuances", t =>
            {
                t.HasCheckConstraint("CK_1A_issuance_clock", "\"AsOfUtc\" <= \"KnowledgeCutoffUtc\" AND \"KnowledgeCutoffUtc\" <= \"CreatedAtUtc\" AND \"CreatedAtUtc\" <= \"IssuedAtUtc\" AND \"IssuedAtUtc\" <= \"AsOfUtc\" + interval '15 minutes' AND EXTRACT(EPOCH FROM \"AsOfUtc\") % 3600 = 0 AND \"ReferenceBoundaryUtc\" = \"AsOfUtc\" + interval '1 hour'");
            });
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.ModelId, x.AsOfUtc }).IsUnique(); e.HasIndex(x => x.BatchId).IsUnique();
            e.HasAlternateKey(x => new { x.Id, x.BatchId }); e.HasIndex(x => new { x.IssuedAtUtc, x.Id });
            e.Property(x => x.CreatingTransactionId).HasDefaultValueSql("pg_current_xact_id()::text");
            e.HasOne<ScoringBatchRow>().WithMany().HasForeignKey(x => new { x.BatchId, x.AsOfUtc, x.ModelId })
                .HasPrincipalKey(x => new { x.Id, x.AsOfUtc, x.ModelId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ForwardMethodologyRow>().WithMany().HasForeignKey(x => x.MethodologyId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ForwardIssuanceItemRow>(e =>
        {
            e.ToTable("ForwardIssuanceItems", t => t.HasCheckConstraint("CK_1A_rank", "\"Rank\" IS NULL OR \"Rank\" BETWEEN 1 AND 3"));
            e.HasKey(x => new { x.IssuanceId, x.AssetId }); e.HasIndex(x => new { x.IssuanceId, x.Rank }).IsUnique();
            e.HasOne<ForwardIssuanceRow>().WithMany().HasForeignKey(x => new { x.IssuanceId, x.BatchId })
                .HasPrincipalKey(x => new { x.Id, x.BatchId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ScoreSnapshotRow>().WithMany().HasForeignKey(x => new { x.ScoreSnapshotId, x.BatchId })
                .HasPrincipalKey(x => new { x.Id, x.BatchId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ForwardOutcomeTargetRow>(e =>
        {
            e.ToTable("ForwardOutcomeTargets", t => t.HasCheckConstraint("CK_1A_target", "\"HorizonHours\" IN (1,4,24,168) AND EXTRACT(EPOCH FROM \"ReferenceBoundaryUtc\") % 3600 = 0 AND \"MaturityUtc\" = \"ReferenceBoundaryUtc\" + \"HorizonHours\" * interval '1 hour'"));
            e.HasKey(x => new { x.IssuanceId, x.AssetId, x.HorizonHours }); e.HasIndex(x => x.MaturityUtc);
            e.HasOne<ForwardIssuanceItemRow>().WithMany().HasForeignKey(x => new { x.IssuanceId, x.AssetId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<InstrumentRef>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ForwardOutcomeAssessmentRow>(e =>
        {
            e.ToTable("ForwardOutcomeAssessments", t =>
            {
                t.HasCheckConstraint("CK_1A_assessment_clock", "\"KnowledgeCutoffUtc\" <= \"AssessedAtUtc\" AND \"Sequence\" >= 1");
                t.HasCheckConstraint("CK_1A_assessment_state", "(\"State\" = 'complete' AND \"Return\" IS NOT NULL AND \"OriginalCompleteAssessmentId\" IS NULL) OR (\"State\" IN ('pending','incomplete') AND \"Return\" IS NULL AND \"OriginalCompleteAssessmentId\" IS NULL) OR (\"State\" = 'conflicted' AND ((\"Return\" IS NULL AND \"OriginalCompleteAssessmentId\" IS NULL) OR (\"Return\" IS NOT NULL AND \"OriginalCompleteAssessmentId\" IS NOT NULL)))");
                t.HasCheckConstraint("CK_1A_assessment_prices", "(\"Return\" IS NULL AND \"ReferencePrice\" IS NULL AND \"TerminalPrice\" IS NULL) OR (\"Return\" IS NOT NULL AND \"ReferencePrice\" > 0 AND \"TerminalPrice\" > 0 AND \"ReferencePrice\" IS NOT NULL AND \"TerminalPrice\" IS NOT NULL AND \"Return\" >= -1 AND scale(\"Return\") <= 18 AND abs(\"Return\") < 1e28 AND \"Return\" NOT IN ('NaN'::numeric,'Infinity'::numeric,'-Infinity'::numeric))");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.IssuanceId, x.AssetId, x.HorizonHours, x.EvidenceHash }).IsUnique();
            e.HasIndex(x => new { x.IssuanceId, x.AssetId, x.HorizonHours, x.Sequence }).IsUnique();
            e.HasOne<ForwardOutcomeTargetRow>().WithMany().HasForeignKey(x => new { x.IssuanceId, x.AssetId, x.HorizonHours }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ForwardOutcomeAssessmentRow>().WithMany().HasForeignKey(x => x.OriginalCompleteAssessmentId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.CreatingTransactionId).HasDefaultValueSql("pg_current_xact_id()::text");
        });
        model.Entity<ForwardOutcomeInputRow>(e =>
        {
            e.ToTable("ForwardOutcomeInputs"); e.HasKey(x => new { x.AssessmentId, x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.HasOne<ForwardOutcomeAssessmentRow>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ObservationRow>().WithMany().HasForeignKey(x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PayloadRow>().WithMany().HasForeignKey(x => new { x.PayloadId, x.InstrumentId }).HasPrincipalKey(x => new { x.Id, x.InstrumentId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ForwardObservationConflictRow>(e =>
        {
            e.ToTable("ForwardObservationConflicts"); e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.HasOne<ObservationRow>().WithMany().HasForeignKey(x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PayloadRow>().WithMany().HasForeignKey(x => new { x.CandidatePayloadId, x.InstrumentId }).HasPrincipalKey(x => new { x.Id, x.InstrumentId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<QuarantineRow>().WithMany().HasForeignKey(x => x.QuarantineId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ForwardRunEventRow>(e =>
        {
            e.ToTable("ForwardRunEvents", t => t.HasCheckConstraint("CK_1A_run_state", "\"State\" IN ('started','completed','failed','cancelled') AND \"EndUtc\" > \"StartUtc\""));
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.RunId, x.State }).IsUnique(); e.HasIndex(x => x.AtUtc);
        });
    }
}
