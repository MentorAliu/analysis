using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analysis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Roadmap1AForwardSignalsOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForwardMethodologies",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    ManifestHash = table.Column<string>(type: "text", nullable: false),
                    SourceHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardMethodologies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForwardObservationConflicts",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    QuarantineId = table.Column<string>(type: "character varying(64)", nullable: false),
                    CandidatePayloadId = table.Column<string>(type: "character varying(64)", nullable: false),
                    OriginalFactJson = table.Column<string>(type: "text", nullable: false),
                    CandidateJson = table.Column<string>(type: "text", nullable: false),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardObservationConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForwardObservationConflicts_Observations_InstrumentId_Kind_~",
                        columns: x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds },
                        principalSchema: "research",
                        principalTable: "Observations",
                        principalColumns: new[] { "InstrumentId", "Kind", "EventTimeUtc", "PeriodSeconds" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardObservationConflicts_ProviderPayloads_CandidatePaylo~",
                        columns: x => new { x.CandidatePayloadId, x.InstrumentId },
                        principalSchema: "research",
                        principalTable: "ProviderPayloads",
                        principalColumns: new[] { "Id", "InstrumentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardObservationConflicts_Quarantine_QuarantineId",
                        column: x => x.QuarantineId,
                        principalSchema: "research",
                        principalTable: "Quarantine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForwardRunEvents",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RunId = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardRunEvents", x => x.Id);
                    table.CheckConstraint("CK_1A_run_state", "\"State\" IN ('started','completed','failed','cancelled') AND \"EndUtc\" > \"StartUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "ForwardIssuances",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(64)", nullable: false),
                    MethodologyId = table.Column<string>(type: "text", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KnowledgeCutoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReferenceBoundaryUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginalJson = table.Column<string>(type: "text", nullable: false),
                    OriginalHash = table.Column<string>(type: "text", nullable: false),
                    CreatingTransactionId = table.Column<string>(type: "text", nullable: false, defaultValueSql: "pg_current_xact_id()::text")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardIssuances", x => x.Id);
                    table.UniqueConstraint("AK_ForwardIssuances_Id_BatchId", x => new { x.Id, x.BatchId });
                    table.CheckConstraint("CK_1A_issuance_clock", "\"AsOfUtc\" <= \"KnowledgeCutoffUtc\" AND \"KnowledgeCutoffUtc\" <= \"CreatedAtUtc\" AND \"CreatedAtUtc\" <= \"IssuedAtUtc\" AND \"IssuedAtUtc\" <= \"AsOfUtc\" + interval '15 minutes' AND EXTRACT(EPOCH FROM \"AsOfUtc\") % 3600 = 0 AND \"ReferenceBoundaryUtc\" = \"AsOfUtc\" + interval '1 hour'");
                    table.ForeignKey(
                        name: "FK_ForwardIssuances_ForwardMethodologies_MethodologyId",
                        column: x => x.MethodologyId,
                        principalSchema: "research",
                        principalTable: "ForwardMethodologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardIssuances_ScoringBatches_BatchId_AsOfUtc_ModelId",
                        columns: x => new { x.BatchId, x.AsOfUtc, x.ModelId },
                        principalSchema: "research",
                        principalTable: "ScoringBatches",
                        principalColumns: new[] { "Id", "AsOfUtc", "ModelId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForwardIssuanceItems",
                schema: "research",
                columns: table => new
                {
                    IssuanceId = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(32)", nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    ScoreSnapshotId = table.Column<string>(type: "character varying(64)", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    ItemJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardIssuanceItems", x => new { x.IssuanceId, x.AssetId });
                    table.CheckConstraint("CK_1A_rank", "\"Rank\" IS NULL OR \"Rank\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ForwardIssuanceItems_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "research",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardIssuanceItems_ForwardIssuances_IssuanceId_BatchId",
                        columns: x => new { x.IssuanceId, x.BatchId },
                        principalSchema: "research",
                        principalTable: "ForwardIssuances",
                        principalColumns: new[] { "Id", "BatchId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardIssuanceItems_ScoreSnapshots_ScoreSnapshotId_BatchId",
                        columns: x => new { x.ScoreSnapshotId, x.BatchId },
                        principalSchema: "research",
                        principalTable: "ScoreSnapshots",
                        principalColumns: new[] { "Id", "BatchId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForwardOutcomeTargets",
                schema: "research",
                columns: table => new
                {
                    IssuanceId = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(32)", nullable: false),
                    HorizonHours = table.Column<int>(type: "integer", nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    ReferenceBoundaryUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaturityUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardOutcomeTargets", x => new { x.IssuanceId, x.AssetId, x.HorizonHours });
                    table.CheckConstraint("CK_1A_target", "\"HorizonHours\" IN (1,4,24,168) AND EXTRACT(EPOCH FROM \"ReferenceBoundaryUtc\") % 3600 = 0 AND \"MaturityUtc\" = \"ReferenceBoundaryUtc\" + \"HorizonHours\" * interval '1 hour'");
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeTargets_ForwardIssuanceItems_IssuanceId_Asset~",
                        columns: x => new { x.IssuanceId, x.AssetId },
                        principalSchema: "research",
                        principalTable: "ForwardIssuanceItems",
                        principalColumns: new[] { "IssuanceId", "AssetId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeTargets_ProviderInstrumentRefs_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "research",
                        principalTable: "ProviderInstrumentRefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForwardOutcomeAssessments",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IssuanceId = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(32)", nullable: false),
                    HorizonHours = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AssessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KnowledgeCutoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ReferencePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    TerminalPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Return = table.Column<decimal>(type: "numeric", nullable: true),
                    OriginalCompleteAssessmentId = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceHash = table.Column<string>(type: "text", nullable: false),
                    CreatingTransactionId = table.Column<string>(type: "text", nullable: false, defaultValueSql: "pg_current_xact_id()::text")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardOutcomeAssessments", x => x.Id);
                    table.CheckConstraint("CK_1A_assessment_clock", "\"KnowledgeCutoffUtc\" <= \"AssessedAtUtc\" AND \"Sequence\" >= 1");
                    table.CheckConstraint("CK_1A_assessment_prices", "(\"Return\" IS NULL AND \"ReferencePrice\" IS NULL AND \"TerminalPrice\" IS NULL) OR (\"Return\" IS NOT NULL AND \"ReferencePrice\" > 0 AND \"TerminalPrice\" > 0 AND \"ReferencePrice\" IS NOT NULL AND \"TerminalPrice\" IS NOT NULL AND \"Return\" >= -1 AND scale(\"Return\") <= 18 AND abs(\"Return\") < 1e28 AND \"Return\" NOT IN ('NaN'::numeric,'Infinity'::numeric,'-Infinity'::numeric))");
                    table.CheckConstraint("CK_1A_assessment_state", "(\"State\" = 'complete' AND \"Return\" IS NOT NULL AND \"OriginalCompleteAssessmentId\" IS NULL) OR (\"State\" IN ('pending','incomplete') AND \"Return\" IS NULL AND \"OriginalCompleteAssessmentId\" IS NULL) OR (\"State\" = 'conflicted' AND ((\"Return\" IS NULL AND \"OriginalCompleteAssessmentId\" IS NULL) OR (\"Return\" IS NOT NULL AND \"OriginalCompleteAssessmentId\" IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeAssessments_ForwardOutcomeAssessments_Origina~",
                        column: x => x.OriginalCompleteAssessmentId,
                        principalSchema: "research",
                        principalTable: "ForwardOutcomeAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeAssessments_ForwardOutcomeTargets_IssuanceId_~",
                        columns: x => new { x.IssuanceId, x.AssetId, x.HorizonHours },
                        principalSchema: "research",
                        principalTable: "ForwardOutcomeTargets",
                        principalColumns: new[] { "IssuanceId", "AssetId", "HorizonHours" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForwardOutcomeInputs",
                schema: "research",
                columns: table => new
                {
                    AssessmentId = table.Column<string>(type: "text", nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    PayloadId = table.Column<string>(type: "character varying(64)", nullable: false),
                    FactJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardOutcomeInputs", x => new { x.AssessmentId, x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds });
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeInputs_ForwardOutcomeAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "research",
                        principalTable: "ForwardOutcomeAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeInputs_Observations_InstrumentId_Kind_EventTi~",
                        columns: x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds },
                        principalSchema: "research",
                        principalTable: "Observations",
                        principalColumns: new[] { "InstrumentId", "Kind", "EventTimeUtc", "PeriodSeconds" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardOutcomeInputs_ProviderPayloads_PayloadId_InstrumentId",
                        columns: x => new { x.PayloadId, x.InstrumentId },
                        principalSchema: "research",
                        principalTable: "ProviderPayloads",
                        principalColumns: new[] { "Id", "InstrumentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuanceItems_AssetId",
                schema: "research",
                table: "ForwardIssuanceItems",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuanceItems_IssuanceId_BatchId",
                schema: "research",
                table: "ForwardIssuanceItems",
                columns: new[] { "IssuanceId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuanceItems_IssuanceId_Rank",
                schema: "research",
                table: "ForwardIssuanceItems",
                columns: new[] { "IssuanceId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuanceItems_ScoreSnapshotId_BatchId",
                schema: "research",
                table: "ForwardIssuanceItems",
                columns: new[] { "ScoreSnapshotId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuances_BatchId",
                schema: "research",
                table: "ForwardIssuances",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuances_BatchId_AsOfUtc_ModelId",
                schema: "research",
                table: "ForwardIssuances",
                columns: new[] { "BatchId", "AsOfUtc", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuances_IssuedAtUtc_Id",
                schema: "research",
                table: "ForwardIssuances",
                columns: new[] { "IssuedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuances_MethodologyId",
                schema: "research",
                table: "ForwardIssuances",
                column: "MethodologyId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardIssuances_ModelId_AsOfUtc",
                schema: "research",
                table: "ForwardIssuances",
                columns: new[] { "ModelId", "AsOfUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForwardObservationConflicts_CandidatePayloadId_InstrumentId",
                schema: "research",
                table: "ForwardObservationConflicts",
                columns: new[] { "CandidatePayloadId", "InstrumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardObservationConflicts_InstrumentId_Kind_EventTimeUtc_~",
                schema: "research",
                table: "ForwardObservationConflicts",
                columns: new[] { "InstrumentId", "Kind", "EventTimeUtc", "PeriodSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardObservationConflicts_QuarantineId",
                schema: "research",
                table: "ForwardObservationConflicts",
                column: "QuarantineId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeAssessments_IssuanceId_AssetId_HorizonHours_E~",
                schema: "research",
                table: "ForwardOutcomeAssessments",
                columns: new[] { "IssuanceId", "AssetId", "HorizonHours", "EvidenceHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeAssessments_IssuanceId_AssetId_HorizonHours_S~",
                schema: "research",
                table: "ForwardOutcomeAssessments",
                columns: new[] { "IssuanceId", "AssetId", "HorizonHours", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeAssessments_OriginalCompleteAssessmentId",
                schema: "research",
                table: "ForwardOutcomeAssessments",
                column: "OriginalCompleteAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeInputs_InstrumentId_Kind_EventTimeUtc_PeriodS~",
                schema: "research",
                table: "ForwardOutcomeInputs",
                columns: new[] { "InstrumentId", "Kind", "EventTimeUtc", "PeriodSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeInputs_PayloadId_InstrumentId",
                schema: "research",
                table: "ForwardOutcomeInputs",
                columns: new[] { "PayloadId", "InstrumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeTargets_InstrumentId",
                schema: "research",
                table: "ForwardOutcomeTargets",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardOutcomeTargets_MaturityUtc",
                schema: "research",
                table: "ForwardOutcomeTargets",
                column: "MaturityUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardRunEvents_AtUtc",
                schema: "research",
                table: "ForwardRunEvents",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardRunEvents_RunId_State",
                schema: "research",
                table: "ForwardRunEvents",
                columns: new[] { "RunId", "State" },
                unique: true);
            // EF owns the frozen 1A clock, integrity and append-only guards.
            migrationBuilder.Sql("""

                CREATE FUNCTION research."ForwardClock"() RETURNS timestamptz LANGUAGE sql VOLATILE AS $$
                    SELECT date_trunc('milliseconds', clock_timestamp())
                $$;
                CREATE FUNCTION research."ForwardImmutable"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'forward-immutable-record' USING ERRCODE = '23514'; END $$;
                CREATE FUNCTION research."ForwardInsertClock"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE field record;
                BEGIN
                    FOR field IN SELECT * FROM jsonb_each(to_jsonb(NEW)) LOOP
                        IF field.key LIKE '%Utc' AND field.value <> 'null'::jsonb AND
                           (field.value #>> '{}')::timestamptz <> date_trunc('milliseconds', (field.value #>> '{}')::timestamptz) THEN
                            RAISE EXCEPTION 'forward-millisecond-clock-required' USING ERRCODE = '23514';
                        END IF;
                    END LOOP;
                    IF TG_TABLE_NAME IN ('ForwardIssuances','ForwardOutcomeAssessments') THEN
                        NEW."CreatingTransactionId" := pg_current_xact_id()::text;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE FUNCTION research."ForwardSeal"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE b research."ScoringBatches"; j jsonb; n timestamptz;
                BEGIN
                    SELECT * INTO STRICT b FROM research."ScoringBatches" WHERE "Id" = NEW."BatchId";
                    n := research."ForwardClock"(); j := NEW."OriginalJson"::jsonb;
                    IF b."CreatingTransactionId" <> pg_current_xact_id()::text OR
                       b."KnowledgeCutoffUtc" <> NEW."KnowledgeCutoffUtc" OR b."CreatedAtUtc" <> NEW."CreatedAtUtc" OR
                       NEW."IssuedAtUtc" > n OR NEW."IssuedAtUtc" < n - interval '2 seconds' OR
                       n > NEW."AsOfUtc" + interval '15 minutes' OR
                       j->>'id' IS DISTINCT FROM NEW."Id" OR j->>'batchId' IS DISTINCT FROM NEW."BatchId" OR
                       j->>'recordKind' IS DISTINCT FROM 'forward-issued-ranking' OR
                       j->>'methodologyId' IS DISTINCT FROM NEW."MethodologyId" OR
                       (j->>'asOfUtc')::timestamptz IS DISTINCT FROM NEW."AsOfUtc" OR
                       (j->>'knowledgeCutoffUtc')::timestamptz IS DISTINCT FROM NEW."KnowledgeCutoffUtc" OR
                       (j->>'createdAtUtc')::timestamptz IS DISTINCT FROM NEW."CreatedAtUtc" OR
                       (j->>'issuedAtUtc')::timestamptz IS DISTINCT FROM NEW."IssuedAtUtc" OR
                       (j->>'referenceBoundaryUtc')::timestamptz IS DISTINCT FROM NEW."ReferenceBoundaryUtc" OR
                       j->'universeAssetIds' IS DISTINCT FROM '["bitcoin","ethereum","solana"]'::jsonb OR
                       jsonb_array_length(j->'items') IS DISTINCT FROM 3 THEN
                        RAISE EXCEPTION 'forward-invalid-seal' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE FUNCTION research."ForwardIssuanceChild"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent research."ForwardIssuances";
                BEGIN
                    SELECT * INTO STRICT parent FROM research."ForwardIssuances" WHERE "Id" = NEW."IssuanceId";
                    IF parent."CreatingTransactionId" <> pg_current_xact_id()::text THEN
                        RAISE EXCEPTION 'forward-issuance-already-sealed' USING ERRCODE = '23514';
                    END IF;
                    IF TG_TABLE_NAME = 'ForwardIssuanceItems' THEN
                        IF NOT EXISTS (SELECT 1 FROM research."ScoreSnapshots" s WHERE s."Id" = NEW."ScoreSnapshotId"
                              AND s."BatchId" = parent."BatchId" AND s."AssetId" = NEW."AssetId") OR
                           NOT EXISTS (SELECT 1 FROM jsonb_array_elements(parent."OriginalJson"::jsonb->'items') item
                              WHERE item = NEW."ItemJson"::jsonb AND item->'asset'->>'id' = NEW."AssetId"
                              AND (item->>'rank')::integer IS NOT DISTINCT FROM NEW."Rank") THEN
                            RAISE EXCEPTION 'forward-invalid-item-lineage' USING ERRCODE = '23514';
                        END IF;
                    ELSE
                        IF NEW."ReferenceBoundaryUtc" <> parent."ReferenceBoundaryUtc" OR
                           NOT EXISTS (SELECT 1 FROM research."ProviderInstrumentRefs" i WHERE i."Id" = NEW."InstrumentId"
                             AND i."AssetId" = NEW."AssetId" AND i."ProviderId" = 'binance' AND i."Kind" = 'Spot' AND i."QuoteUnit" = 'USDT') THEN
                            RAISE EXCEPTION 'forward-invalid-target-lineage' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE FUNCTION research."ForwardCompleteIssuance"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF research."ForwardClock"() > NEW."AsOfUtc" + interval '15 minutes' OR
                       research."ForwardClock"() < NEW."IssuedAtUtc" OR
                       (SELECT count(*) FROM research."ForwardIssuanceItems" WHERE "IssuanceId" = NEW."Id") <> 3 OR
                       (SELECT count(*) FROM research."ForwardOutcomeTargets" WHERE "IssuanceId" = NEW."Id") <> 12 OR
                       EXISTS (SELECT 1 FROM research."ForwardIssuanceItems" i WHERE i."IssuanceId" = NEW."Id" AND
                         (SELECT count(*) FROM research."ForwardOutcomeTargets" t WHERE t."IssuanceId" = i."IssuanceId" AND t."AssetId" = i."AssetId") <> 4) THEN
                        RAISE EXCEPTION 'forward-incomplete-issuance' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE FUNCTION research."ForwardAssessment"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE t research."ForwardOutcomeTargets"; prior research."ForwardOutcomeAssessments"; j jsonb;
                BEGIN
                    SELECT * INTO STRICT t FROM research."ForwardOutcomeTargets" WHERE "IssuanceId" = NEW."IssuanceId"
                        AND "AssetId" = NEW."AssetId" AND "HorizonHours" = NEW."HorizonHours";
                    j := NEW."ResultJson"::jsonb;
                    IF EXISTS (SELECT 1 FROM (VALUES (NEW."ReferencePrice"), (NEW."TerminalPrice"), (NEW."Return")) v(n)
                        WHERE n IS NOT NULL AND (n IN ('NaN'::numeric,'Infinity'::numeric,'-Infinity'::numeric) OR
                          scale(trim_scale(n)) > 18 OR length(replace(ltrim(trim_scale(abs(n))::text,'0'),'.','')) > 28)) THEN
                        RAISE EXCEPTION 'forward-invalid-numeric-range' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."AssessedAtUtc" > research."ForwardClock"() OR
                       (NEW."State" = 'pending') IS DISTINCT FROM (NEW."KnowledgeCutoffUtc" < t."MaturityUtc") OR
                       NEW."KnowledgeCutoffUtc" < (SELECT "IssuedAtUtc" FROM research."ForwardIssuances" WHERE "Id" = NEW."IssuanceId") OR
                       j->>'state' IS DISTINCT FROM NEW."State" OR j->>'quoteUnit' IS DISTINCT FROM 'USDT' OR
                       j->>'returnUnit' IS DISTINCT FROM 'fraction' OR
                       j->'target'->>'issuanceId' IS DISTINCT FROM NEW."IssuanceId" OR
                       j->'target'->>'assetId' IS DISTINCT FROM NEW."AssetId" OR
                       j->'target'->>'instrumentId' IS DISTINCT FROM t."InstrumentId" OR
                       (j->'target'->>'horizonHours')::integer IS DISTINCT FROM NEW."HorizonHours" OR
                       (j->'target'->>'referenceBoundaryUtc')::timestamptz IS DISTINCT FROM t."ReferenceBoundaryUtc" OR
                       (j->'target'->>'maturityUtc')::timestamptz IS DISTINCT FROM t."MaturityUtc" OR
                       (j->>'return')::numeric IS DISTINCT FROM NEW."Return" OR
                       (j->>'referencePrice')::numeric IS DISTINCT FROM NEW."ReferencePrice" OR
                       (j->>'terminalPrice')::numeric IS DISTINCT FROM NEW."TerminalPrice" THEN
                        RAISE EXCEPTION 'forward-invalid-assessment' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."OriginalCompleteAssessmentId" IS NOT NULL THEN
                        SELECT * INTO STRICT prior FROM research."ForwardOutcomeAssessments" WHERE "Id" = NEW."OriginalCompleteAssessmentId";
                        IF prior."State" <> 'complete' OR prior."IssuanceId" <> NEW."IssuanceId" OR prior."AssetId" <> NEW."AssetId" OR
                           prior."HorizonHours" <> NEW."HorizonHours" OR prior."Sequence" >= NEW."Sequence" OR
                           prior."Return" IS DISTINCT FROM NEW."Return" OR prior."ReferencePrice" IS DISTINCT FROM NEW."ReferencePrice" OR
                           prior."TerminalPrice" IS DISTINCT FROM NEW."TerminalPrice" THEN
                            RAISE EXCEPTION 'forward-invalid-original-measurement' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE FUNCTION research."ForwardOutcomeChild"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent research."ForwardOutcomeAssessments";
                BEGIN
                    SELECT * INTO STRICT parent FROM research."ForwardOutcomeAssessments" WHERE "Id" = NEW."AssessmentId";
                    IF parent."CreatingTransactionId" <> pg_current_xact_id()::text THEN
                        RAISE EXCEPTION 'forward-outcome-already-sealed' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."Kind" <> 'Candle' OR NEW."PeriodSeconds" <> 3600 OR
                       NEW."EventTimeUtc" + interval '1 hour' > parent."KnowledgeCutoffUtc" OR
                       NOT EXISTS (SELECT 1 FROM research."Observations" o WHERE o."InstrumentId" = NEW."InstrumentId"
                         AND o."Kind" = NEW."Kind" AND o."EventTimeUtc" = NEW."EventTimeUtc" AND o."PeriodSeconds" = NEW."PeriodSeconds"
                         AND o."PayloadId" = NEW."PayloadId") OR
                       NOT EXISTS (SELECT 1 FROM jsonb_array_elements(parent."ResultJson"::jsonb->'evidence'->'facts') fact
                         WHERE fact = NEW."FactJson"::jsonb AND fact->>'payloadId' = NEW."PayloadId"
                           AND fact->'observation'->>'instrumentId' = NEW."InstrumentId"
                           AND (fact->'observation'->>'eventTimeUtc')::timestamptz = NEW."EventTimeUtc") THEN
                        RAISE EXCEPTION 'forward-invalid-outcome-input' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE FUNCTION research."ForwardCompleteAssessment"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE expected integer; target research."ForwardOutcomeTargets";
                BEGIN
                    expected := jsonb_array_length(NEW."ResultJson"::jsonb->'evidence'->'facts');
                    SELECT * INTO STRICT target FROM research."ForwardOutcomeTargets" WHERE "IssuanceId" = NEW."IssuanceId"
                        AND "AssetId" = NEW."AssetId" AND "HorizonHours" = NEW."HorizonHours";
                    IF expected IS NULL OR expected <> (SELECT count(*) FROM research."ForwardOutcomeInputs" WHERE "AssessmentId" = NEW."Id") OR
                       EXISTS (SELECT 1 FROM research."ForwardOutcomeInputs" i WHERE i."AssessmentId" = NEW."Id" AND
                         (i."InstrumentId" <> target."InstrumentId" OR i."EventTimeUtc" < target."ReferenceBoundaryUtc" - interval '1 hour'
                          OR i."EventTimeUtc" >= target."MaturityUtc")) OR
                       (NEW."Return" IS NOT NULL AND expected <> NEW."HorizonHours" + 1) THEN
                        RAISE EXCEPTION 'forward-incomplete-assessment' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE UNIQUE INDEX "IX_1A_first_complete" ON research."ForwardOutcomeAssessments" ("IssuanceId","AssetId","HorizonHours") WHERE "State" = 'complete';
                CREATE TRIGGER "ForwardSeal" BEFORE INSERT ON research."ForwardIssuances" FOR EACH ROW EXECUTE FUNCTION research."ForwardSeal"();
                CREATE CONSTRAINT TRIGGER "ForwardCompleteIssuance" AFTER INSERT ON research."ForwardIssuances" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION research."ForwardCompleteIssuance"();
                CREATE TRIGGER "ForwardAssessment" BEFORE INSERT ON research."ForwardOutcomeAssessments" FOR EACH ROW EXECUTE FUNCTION research."ForwardAssessment"();
                CREATE CONSTRAINT TRIGGER "ForwardCompleteAssessment" AFTER INSERT ON research."ForwardOutcomeAssessments" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION research."ForwardCompleteAssessment"();
                CREATE TRIGGER "ForwardOutcomeChild" BEFORE INSERT ON research."ForwardOutcomeInputs" FOR EACH ROW EXECUTE FUNCTION research."ForwardOutcomeChild"();
                CREATE TRIGGER "ForwardIssuanceChild" BEFORE INSERT ON research."ForwardIssuanceItems" FOR EACH ROW EXECUTE FUNCTION research."ForwardIssuanceChild"();
                CREATE TRIGGER "ForwardIssuanceChild" BEFORE INSERT ON research."ForwardOutcomeTargets" FOR EACH ROW EXECUTE FUNCTION research."ForwardIssuanceChild"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardMethodologies" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardMethodologies" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardMethodologies" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardIssuances" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardIssuances" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardIssuances" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardIssuanceItems" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardIssuanceItems" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardIssuanceItems" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardOutcomeTargets" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardOutcomeTargets" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardOutcomeTargets" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardOutcomeAssessments" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardOutcomeAssessments" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardOutcomeAssessments" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardOutcomeInputs" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardOutcomeInputs" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardOutcomeInputs" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardObservationConflicts" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardObservationConflicts" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardObservationConflicts" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardInsertClock" BEFORE INSERT ON research."ForwardRunEvents" FOR EACH ROW EXECUTE FUNCTION research."ForwardInsertClock"();
                CREATE TRIGGER "ForwardImmutableRows" BEFORE UPDATE OR DELETE ON research."ForwardRunEvents" FOR EACH ROW EXECUTE FUNCTION research."ForwardImmutable"();
                CREATE TRIGGER "ForwardImmutableTruncate" BEFORE TRUNCATE ON research."ForwardRunEvents" FOR EACH STATEMENT EXECUTE FUNCTION research."ForwardImmutable"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForwardObservationConflicts",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardOutcomeInputs",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardRunEvents",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardOutcomeAssessments",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardOutcomeTargets",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardIssuanceItems",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardIssuances",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ForwardMethodologies",
                schema: "research");
            migrationBuilder.Sql("""
                DROP FUNCTION research."ForwardCompleteAssessment"();
                DROP FUNCTION research."ForwardOutcomeChild"();
                DROP FUNCTION research."ForwardAssessment"();
                DROP FUNCTION research."ForwardCompleteIssuance"();
                DROP FUNCTION research."ForwardIssuanceChild"();
                DROP FUNCTION research."ForwardSeal"();
                DROP FUNCTION research."ForwardInsertClock"();
                DROP FUNCTION research."ForwardImmutable"();
                DROP FUNCTION research."ForwardClock"();
                """);
        }
    }
}
