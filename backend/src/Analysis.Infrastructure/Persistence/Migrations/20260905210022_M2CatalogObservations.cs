using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Analysis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M2CatalogObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "research");

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderInstrumentRefs",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(32)", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(32)", nullable: false),
                    NativeSymbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BaseUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    QuoteUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SettlementUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderInstrumentRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderInstrumentRefs_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "research",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderInstrumentRefs_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "research",
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderPayloads",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    MappingVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    WindowStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderPayloads", x => x.Id);
                    table.UniqueConstraint("AK_ProviderPayloads_Id_InstrumentId", x => new { x.Id, x.InstrumentId });
                    table.ForeignKey(
                        name: "FK_ProviderPayloads_ProviderInstrumentRefs_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "research",
                        principalTable: "ProviderInstrumentRefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quarantine",
                schema: "research",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    WindowStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quarantine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quarantine_ProviderInstrumentRefs_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "research",
                        principalTable: "ProviderInstrumentRefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Observations",
                schema: "research",
                columns: table => new
                {
                    InstrumentId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    QuoteUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Open = table.Column<decimal>(type: "numeric", nullable: true),
                    High = table.Column<decimal>(type: "numeric", nullable: true),
                    Low = table.Column<decimal>(type: "numeric", nullable: true),
                    Close = table.Column<decimal>(type: "numeric", nullable: true),
                    Volume = table.Column<decimal>(type: "numeric", nullable: true),
                    QuoteVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    Value = table.Column<decimal>(type: "numeric", nullable: true),
                    PayloadId = table.Column<string>(type: "character varying(64)", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Observations", x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds });
                    table.CheckConstraint("CK_Observation_kind", "\"Kind\" IN ('Candle', 'FundingRate', 'OpenInterestBothSides', 'ChainTvl')");
                    table.CheckConstraint("CK_Observation_period", "\"PeriodSeconds\" >= 0");
                    table.CheckConstraint("CK_Observation_scalar", "\"Kind\" = 'Candle' OR (\"Kind\" = 'FundingRate' AND \"Unit\" = 'fraction' AND \"Value\" BETWEEN -1 AND 1 AND \"PeriodSeconds\" = 0) OR (\"Kind\" = 'OpenInterestBothSides' AND \"Value\" >= 0 AND \"PeriodSeconds\" = 3600) OR (\"Kind\" = 'ChainTvl' AND \"Unit\" = 'USD' AND \"Value\" >= 0 AND \"PeriodSeconds\" = 0)");
                    table.CheckConstraint("CK_Observation_shape", "(\"Kind\" = 'Candle' AND \"Open\" IS NOT NULL AND \"High\" IS NOT NULL AND \"Low\" IS NOT NULL AND \"Close\" IS NOT NULL AND \"Volume\" IS NOT NULL AND \"QuoteVolume\" IS NOT NULL AND \"QuoteUnit\" IS NOT NULL AND \"Open\" > 0 AND \"Low\" > 0 AND \"Open\" BETWEEN \"Low\" AND \"High\" AND \"Close\" BETWEEN \"Low\" AND \"High\" AND \"Volume\" >= 0 AND \"QuoteVolume\" >= 0 AND \"Value\" IS NULL AND \"PeriodSeconds\" = 3600) OR (\"Kind\" <> 'Candle' AND \"Value\" IS NOT NULL AND \"Open\" IS NULL AND \"High\" IS NULL AND \"Low\" IS NULL AND \"Close\" IS NULL AND \"Volume\" IS NULL AND \"QuoteVolume\" IS NULL AND \"QuoteUnit\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Observations_ProviderInstrumentRefs_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "research",
                        principalTable: "ProviderInstrumentRefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Observations_ProviderPayloads_PayloadId_InstrumentId",
                        columns: x => new { x.PayloadId, x.InstrumentId },
                        principalSchema: "research",
                        principalTable: "ProviderPayloads",
                        principalColumns: new[] { "Id", "InstrumentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "research",
                table: "Assets",
                columns: new[] { "Id", "Name", "Symbol" },
                values: new object[,]
                {
                    { "bitcoin", "Bitcoin", "BTC" },
                    { "ethereum", "Ether", "ETH" },
                    { "solana", "Solana", "SOL" }
                });

            migrationBuilder.InsertData(
                schema: "research",
                table: "Providers",
                columns: new[] { "Id", "ApprovalStatus" },
                values: new object[,]
                {
                    { "binance", "Unresolved" },
                    { "bybit", "Unresolved" },
                    { "defillama", "Unresolved" }
                });

            migrationBuilder.InsertData(
                schema: "research",
                table: "ProviderInstrumentRefs",
                columns: new[] { "Id", "AssetId", "BaseUnit", "Kind", "NativeSymbol", "ProviderId", "QuoteUnit", "SettlementUnit" },
                values: new object[,]
                {
                    { "binance:spot:BTCUSDT", "bitcoin", "BTC", "Spot", "BTCUSDT", "binance", "USDT", null },
                    { "binance:spot:ETHUSDT", "ethereum", "ETH", "Spot", "ETHUSDT", "binance", "USDT", null },
                    { "binance:spot:SOLUSDT", "solana", "SOL", "Spot", "SOLUSDT", "binance", "USDT", null },
                    { "bybit:linear:BTCUSDT", "bitcoin", "BTC", "LinearPerpetual", "BTCUSDT", "bybit", "USDT", "USDT" },
                    { "bybit:linear:ETHUSDT", "ethereum", "ETH", "LinearPerpetual", "ETHUSDT", "bybit", "USDT", "USDT" },
                    { "bybit:linear:SOLUSDT", "solana", "SOL", "LinearPerpetual", "SOLUSDT", "bybit", "USDT", "USDT" },
                    { "defillama:chain:Ethereum", "ethereum", "ETH", "Chain", "Ethereum", "defillama", null, null },
                    { "defillama:chain:Solana", "solana", "SOL", "Chain", "Solana", "defillama", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Observations_PayloadId_InstrumentId",
                schema: "research",
                table: "Observations",
                columns: new[] { "PayloadId", "InstrumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInstrumentRefs_AssetId",
                schema: "research",
                table: "ProviderInstrumentRefs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInstrumentRefs_ProviderId_Kind_NativeSymbol",
                schema: "research",
                table: "ProviderInstrumentRefs",
                columns: new[] { "ProviderId", "Kind", "NativeSymbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPayloads_InstrumentId",
                schema: "research",
                table: "ProviderPayloads",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Quarantine_InstrumentId",
                schema: "research",
                table: "Quarantine",
                column: "InstrumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Observations",
                schema: "research");

            migrationBuilder.DropTable(
                name: "Quarantine",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ProviderPayloads",
                schema: "research");

            migrationBuilder.DropTable(
                name: "ProviderInstrumentRefs",
                schema: "research");

            migrationBuilder.DropTable(
                name: "Assets",
                schema: "research");

            migrationBuilder.DropTable(
                name: "Providers",
                schema: "research");
        }
    }
}
