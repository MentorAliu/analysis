using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analysis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M3FeaturesScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoringModels",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    ManifestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoringBatches",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(64)", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KnowledgeCutoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatingTransactionId = table.Column<string>(type: "text", nullable: false, defaultValueSql: "pg_current_xact_id()::text"),
                    RecordKind = table.Column<string>(type: "text", nullable: false),
                    UniverseJson = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    InputHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringBatches", x => x.Id);
                    table.UniqueConstraint("AK_ScoringBatches_Id_AsOfUtc_ModelId", x => new { x.Id, x.AsOfUtc, x.ModelId });
                    table.CheckConstraint("CK_M3_batch_clock", "\"KnowledgeCutoffUtc\" >= \"AsOfUtc\" AND \"CreatedAtUtc\" >= \"KnowledgeCutoffUtc\" AND EXTRACT(EPOCH FROM \"AsOfUtc\") % 3600 = 0");
                    table.CheckConstraint("CK_M3_record_kind", "\"RecordKind\" = 'research-reconstruction'");
                    table.ForeignKey(
                        name: "FK_ScoringBatches_ScoringModels_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "research",
                        principalTable: "ScoringModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeatureSnapshots",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(32)", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(64)", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorePriceReady = table.Column<bool>(type: "boolean", nullable: false),
                    FeatureHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_FeatureSnapshots_Id_BatchId", x => new { x.Id, x.BatchId });
                    table.UniqueConstraint("AK_FeatureSnapshots_Id_BatchId_AssetId_AsOfUtc_ModelId", x => new { x.Id, x.BatchId, x.AssetId, x.AsOfUtc, x.ModelId });
                    table.ForeignKey(
                        name: "FK_FeatureSnapshots_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "research",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeatureSnapshots_ScoringBatches_BatchId_AsOfUtc_ModelId",
                        columns: x => new { x.BatchId, x.AsOfUtc, x.ModelId },
                        principalSchema: "research",
                        principalTable: "ScoringBatches",
                        principalColumns: new[] { "Id", "AsOfUtc", "ModelId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InputConflicts",
                schema: "research",
                columns: table => new
                {
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    ConflictId = table.Column<string>(type: "character varying(64)", nullable: false),
                    FactJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InputConflicts", x => new { x.BatchId, x.ConflictId });
                    table.ForeignKey(
                        name: "FK_InputConflicts_Quarantine_ConflictId",
                        column: x => x.ConflictId,
                        principalSchema: "research",
                        principalTable: "Quarantine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InputConflicts_ScoringBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "research",
                        principalTable: "ScoringBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InputObservations",
                schema: "research",
                columns: table => new
                {
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    FactJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InputObservations", x => new { x.BatchId, x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds });
                    table.ForeignKey(
                        name: "FK_InputObservations_Observations_InstrumentId_Kind_EventTimeU~",
                        columns: x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds },
                        principalSchema: "research",
                        principalTable: "Observations",
                        principalColumns: new[] { "InstrumentId", "Kind", "EventTimeUtc", "PeriodSeconds" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InputObservations_ScoringBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "research",
                        principalTable: "ScoringBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeatureValues",
                schema: "research",
                columns: table => new
                {
                    SnapshotId = table.Column<string>(type: "character varying(64)", nullable: false),
                    FeatureId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    CalculationVersion = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: true),
                    DetailJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureValues", x => new { x.SnapshotId, x.FeatureId });
                    table.CheckConstraint("CK_M3_feature_id", "\"FeatureId\" BETWEEN 1 AND 21");
                    table.CheckConstraint("CK_M3_feature_numeric", "\"Value\" IS NULL OR (\"Value\" NOT IN ('NaN'::numeric,'Infinity'::numeric,'-Infinity'::numeric) AND scale(\"Value\") <= 18 AND abs(\"Value\") < 1e28)");
                    table.CheckConstraint("CK_M3_feature_state", "(\"State\" = 'available' AND \"Value\" IS NOT NULL) OR (\"State\" IN ('missing','stale','invalid','conflicted','inapplicable') AND \"Value\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_FeatureValues_FeatureSnapshots_SnapshotId_BatchId",
                        columns: x => new { x.SnapshotId, x.BatchId },
                        principalSchema: "research",
                        principalTable: "FeatureSnapshots",
                        principalColumns: new[] { "Id", "BatchId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoreSnapshots",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotId = table.Column<string>(type: "character varying(64)", nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(32)", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(64)", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Composite = table.Column<decimal>(type: "numeric", nullable: true),
                    BullishConfidence = table.Column<decimal>(type: "numeric", nullable: true),
                    BearishConfidence = table.Column<decimal>(type: "numeric", nullable: true),
                    DataQuality = table.Column<decimal>(type: "numeric", nullable: false),
                    ContextCoverage = table.Column<decimal>(type: "numeric", nullable: false),
                    ScoreJson = table.Column<string>(type: "text", nullable: false),
                    ScoreHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_ScoreSnapshots_Id_BatchId", x => new { x.Id, x.BatchId });
                    table.CheckConstraint("CK_M3_score_bounds", "(\"Composite\" IS NULL OR \"Composite\" BETWEEN -100 AND 100) AND (\"BullishConfidence\" IS NULL OR \"BullishConfidence\" BETWEEN 0 AND 100) AND (\"BearishConfidence\" IS NULL OR \"BearishConfidence\" BETWEEN 0 AND 100) AND \"DataQuality\" BETWEEN 0 AND 100 AND \"ContextCoverage\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_M3_score_state", "(\"State\" IN ('complete','partial') AND \"Composite\" IS NOT NULL AND \"BullishConfidence\" IS NOT NULL AND \"BearishConfidence\" IS NOT NULL AND \"DataQuality\" >= 50) OR (\"State\" = 'not-ready' AND \"Composite\" IS NULL AND \"BullishConfidence\" IS NULL AND \"BearishConfidence\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ScoreSnapshots_FeatureSnapshots_SnapshotId_BatchId_AssetId_~",
                        columns: x => new { x.SnapshotId, x.BatchId, x.AssetId, x.AsOfUtc, x.ModelId },
                        principalSchema: "research",
                        principalTable: "FeatureSnapshots",
                        principalColumns: new[] { "Id", "BatchId", "AssetId", "AsOfUtc", "ModelId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoryScores",
                schema: "research",
                columns: table => new
                {
                    ScoreId = table.Column<string>(type: "character varying(64)", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
                    DataQuality = table.Column<decimal>(type: "numeric", nullable: false),
                    ApplicableWeight = table.Column<int>(type: "integer", nullable: false),
                    AvailableWeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryScores", x => new { x.ScoreId, x.Category });
                    table.CheckConstraint("CK_M3_category", "\"Category\" IN ('price','derivatives','fundamentals','regime') AND \"State\" IN ('complete','partial','missing','inapplicable') AND \"DataQuality\" BETWEEN 0 AND 100 AND (\"Score\" IS NULL OR \"Score\" BETWEEN -100 AND 100) AND \"AvailableWeight\" BETWEEN 0 AND \"ApplicableWeight\"");
                    table.CheckConstraint("CK_M3_category_state", "(\"State\" IN ('complete','partial') AND \"Score\" IS NOT NULL) OR (\"State\" IN ('missing','inapplicable') AND \"Score\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_CategoryScores_ScoreSnapshots_ScoreId_BatchId",
                        columns: x => new { x.ScoreId, x.BatchId },
                        principalSchema: "research",
                        principalTable: "ScoreSnapshots",
                        principalColumns: new[] { "Id", "BatchId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryScores_ScoreId_BatchId",
                schema: "research",
                table: "CategoryScores",
                columns: new[] { "ScoreId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSnapshots_AssetId_AsOfUtc_ModelId",
                schema: "research",
                table: "FeatureSnapshots",
                columns: new[] { "AssetId", "AsOfUtc", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSnapshots_BatchId_AsOfUtc_ModelId",
                schema: "research",
                table: "FeatureSnapshots",
                columns: new[] { "BatchId", "AsOfUtc", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureValues_SnapshotId_BatchId",
                schema: "research",
                table: "FeatureValues",
                columns: new[] { "SnapshotId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InputConflicts_ConflictId",
                schema: "research",
                table: "InputConflicts",
                column: "ConflictId");

            migrationBuilder.CreateIndex(
                name: "IX_InputObservations_InstrumentId_Kind_EventTimeUtc_PeriodSeco~",
                schema: "research",
                table: "InputObservations",
                columns: new[] { "InstrumentId", "Kind", "EventTimeUtc", "PeriodSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreSnapshots_AssetId_AsOfUtc_ModelId",
                schema: "research",
                table: "ScoreSnapshots",
                columns: new[] { "AssetId", "AsOfUtc", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreSnapshots_ModelId_AsOfUtc_AssetId",
                schema: "research",
                table: "ScoreSnapshots",
                columns: new[] { "ModelId", "AsOfUtc", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreSnapshots_SnapshotId_BatchId_AssetId_AsOfUtc_ModelId",
                schema: "research",
                table: "ScoreSnapshots",
                columns: new[] { "SnapshotId", "BatchId", "AssetId", "AsOfUtc", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringBatches_ModelId_AsOfUtc",
                schema: "research",
                table: "ScoringBatches",
                columns: new[] { "ModelId", "AsOfUtc" },
                unique: true);

            // EF owns these guards together with the generated tables; no external DDL authority.
            migrationBuilder.Sql("""
                CREATE FUNCTION research."M3_immutable"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'm3-immutable-record' USING ERRCODE = '23514';
                END $$;
                CREATE FUNCTION research."M3_batch_transaction"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    NEW."CreatingTransactionId" := pg_current_xact_id()::text;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "M3_batch_transaction" BEFORE INSERT ON research."ScoringBatches"
                    FOR EACH ROW EXECUTE FUNCTION research."M3_batch_transaction"();
                CREATE FUNCTION research."M3_child_transaction"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM research."ScoringBatches" b WHERE b."Id" = NEW."BatchId"
                        AND b."CreatingTransactionId" = pg_current_xact_id()::text) THEN
                        RAISE EXCEPTION 'm3-snapshot-already-sealed' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE FUNCTION research."M3_complete_batch"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."UniverseJson"::jsonb <> '["bitcoin","ethereum","solana"]'::jsonb OR
                       (SELECT count(*) FROM research."FeatureSnapshots" WHERE "BatchId" = NEW."Id") <> 3 OR
                       (SELECT count(*) FROM research."ScoreSnapshots" WHERE "BatchId" = NEW."Id") <> 3 OR
                       (SELECT count(*) FROM research."FeatureValues" WHERE "BatchId" = NEW."Id") <> 63 OR
                       (SELECT count(*) FROM research."CategoryScores" WHERE "BatchId" = NEW."Id") <> 12 OR
                       EXISTS (SELECT 1 FROM research."FeatureSnapshots" f WHERE f."BatchId" = NEW."Id" AND
                          ((SELECT count(*) FROM research."FeatureValues" v WHERE v."SnapshotId" = f."Id") <> 21 OR
                           (NOT f."CorePriceReady" AND EXISTS (SELECT 1 FROM research."ScoreSnapshots" s
                              WHERE s."SnapshotId" = f."Id" AND s."State" <> 'not-ready')))) THEN
                        RAISE EXCEPTION 'm3-incomplete-batch' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "M3_complete_batch" AFTER INSERT ON research."ScoringBatches"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION research."M3_complete_batch"();
                """);
            foreach (var table in new[] { "ScoringModels", "ScoringBatches", "InputObservations", "InputConflicts",
                "FeatureSnapshots", "FeatureValues", "ScoreSnapshots", "CategoryScores" })
            {
                migrationBuilder.Sql($"CREATE TRIGGER \"M3_immutable_rows\" BEFORE UPDATE OR DELETE ON research.\"{table}\" FOR EACH ROW EXECUTE FUNCTION research.\"M3_immutable\"();");
                migrationBuilder.Sql($"CREATE TRIGGER \"M3_immutable_truncate\" BEFORE TRUNCATE ON research.\"{table}\" FOR EACH STATEMENT EXECUTE FUNCTION research.\"M3_immutable\"();");
                if (table is not ("ScoringModels" or "ScoringBatches"))
                    migrationBuilder.Sql($"CREATE TRIGGER \"M3_child_transaction\" BEFORE INSERT ON research.\"{table}\" FOR EACH ROW EXECUTE FUNCTION research.\"M3_child_transaction\"();");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryScores",
                schema: "research");

            migrationBuilder.DropTable(
                name: "FeatureValues",
                schema: "research");

            migrationBuilder.DropTable(
                name: "InputConflicts",
                schema: "research");

            migrationBuilder.DropTable(
                name: "InputObservations",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ScoreSnapshots",
                schema: "research");

            migrationBuilder.DropTable(
                name: "FeatureSnapshots",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ScoringBatches",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ScoringModels",
                schema: "research");
            migrationBuilder.Sql("""
                DROP FUNCTION research."M3_complete_batch"();
                DROP FUNCTION research."M3_child_transaction"();
                DROP FUNCTION research."M3_batch_transaction"();
                DROP FUNCTION research."M3_immutable"();
                """);
        }
    }
}
