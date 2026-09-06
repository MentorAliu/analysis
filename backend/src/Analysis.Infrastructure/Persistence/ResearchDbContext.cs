using Analysis.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Analysis.Infrastructure.Persistence;

public sealed class ResearchDbContext(DbContextOptions<ResearchDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<ProviderRow> Providers => Set<ProviderRow>();
    public DbSet<InstrumentRef> Instruments => Set<InstrumentRef>();
    public DbSet<PayloadRow> Payloads => Set<PayloadRow>();
    public DbSet<ObservationRow> Observations => Set<ObservationRow>();
    public DbSet<QuarantineRow> Quarantine => Set<QuarantineRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("research");
        ScoringSchema.Configure(model);
        model.Entity<Asset>(e =>
        {
            e.ToTable("Assets"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasData(CatalogSeed.Assets);
        });
        model.Entity<ProviderRow>(e =>
        {
            e.ToTable("Providers"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.ApprovalStatus).HasMaxLength(32);
            e.HasData(new ProviderRow { Id = "binance", ApprovalStatus = "Unresolved" },
                new ProviderRow { Id = "bybit", ApprovalStatus = "Unresolved" },
                new ProviderRow { Id = "defillama", ApprovalStatus = "Unresolved" });
        });
        model.Entity<InstrumentRef>(e =>
        {
            e.ToTable("ProviderInstrumentRefs"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(100);
            e.Property(x => x.NativeSymbol).HasMaxLength(64);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.BaseUnit).HasMaxLength(16);
            e.Property(x => x.QuoteUnit).HasMaxLength(16);
            e.Property(x => x.SettlementUnit).HasMaxLength(16);
            e.HasIndex(x => new { x.ProviderId, x.Kind, x.NativeSymbol }).IsUnique();
            e.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProviderRow>().WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasData(CatalogSeed.Instruments);
        });
        model.Entity<PayloadRow>(e =>
        {
            e.ToTable("ProviderPayloads"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstrumentId });
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.RequestPath).HasMaxLength(2048);
            e.Property(x => x.MappingVersion).HasMaxLength(64);
            e.HasOne<InstrumentRef>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ObservationRow>(e =>
        {
            e.ToTable("Observations", table =>
            {
                table.HasCheckConstraint("CK_Observation_period", "\"PeriodSeconds\" >= 0");
                table.HasCheckConstraint("CK_Observation_kind", "\"Kind\" IN ('Candle', 'FundingRate', 'OpenInterestBothSides', 'ChainTvl')");
                table.HasCheckConstraint("CK_Observation_shape", "(\"Kind\" = 'Candle' AND \"Open\" IS NOT NULL AND \"High\" IS NOT NULL AND \"Low\" IS NOT NULL AND \"Close\" IS NOT NULL AND \"Volume\" IS NOT NULL AND \"QuoteVolume\" IS NOT NULL AND \"QuoteUnit\" IS NOT NULL AND \"Open\" > 0 AND \"Low\" > 0 AND \"Open\" BETWEEN \"Low\" AND \"High\" AND \"Close\" BETWEEN \"Low\" AND \"High\" AND \"Volume\" >= 0 AND \"QuoteVolume\" >= 0 AND \"Value\" IS NULL AND \"PeriodSeconds\" = 3600) OR (\"Kind\" <> 'Candle' AND \"Value\" IS NOT NULL AND \"Open\" IS NULL AND \"High\" IS NULL AND \"Low\" IS NULL AND \"Close\" IS NULL AND \"Volume\" IS NULL AND \"QuoteVolume\" IS NULL AND \"QuoteUnit\" IS NULL)");
                table.HasCheckConstraint("CK_Observation_scalar", "\"Kind\" = 'Candle' OR (\"Kind\" = 'FundingRate' AND \"Unit\" = 'fraction' AND \"Value\" BETWEEN -1 AND 1 AND \"PeriodSeconds\" = 0) OR (\"Kind\" = 'OpenInterestBothSides' AND \"Value\" >= 0 AND \"PeriodSeconds\" = 3600) OR (\"Kind\" = 'ChainTvl' AND \"Unit\" = 'USD' AND \"Value\" >= 0 AND \"PeriodSeconds\" = 0)");
            });
            e.HasKey(x => new { x.InstrumentId, x.Kind, x.EventTimeUtc, x.PeriodSeconds });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Unit).HasMaxLength(16);
            e.Property(x => x.QuoteUnit).HasMaxLength(16);
            e.HasOne<InstrumentRef>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PayloadRow>().WithMany().HasForeignKey(x => new { x.PayloadId, x.InstrumentId })
                .HasPrincipalKey(x => new { x.Id, x.InstrumentId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<QuarantineRow>(e =>
        {
            e.ToTable("Quarantine"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Code).HasMaxLength(80);
            e.HasOne<InstrumentRef>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class ResearchDesignFactory : IDesignTimeDbContextFactory<ResearchDbContext>
{
    public ResearchDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ResearchDbContext>()
        .UseNpgsql("Host=localhost;Database=analysis;Username=analysis", options => options.SetPostgresVersion(18, 0)).Options);
}

public static class CatalogSeed
{
    public static readonly Asset[] Assets = [new("bitcoin", "BTC", "Bitcoin"), new("ethereum", "ETH", "Ether"), new("solana", "SOL", "Solana")];
    public static readonly InstrumentRef[] Instruments = Assets.SelectMany(a => new[]
    {
        new InstrumentRef($"binance:spot:{a.Symbol}USDT", a.Id, "binance", $"{a.Symbol}USDT", InstrumentKind.Spot, a.Symbol, "USDT", null),
        new InstrumentRef($"bybit:linear:{a.Symbol}USDT", a.Id, "bybit", $"{a.Symbol}USDT", InstrumentKind.LinearPerpetual, a.Symbol, "USDT", "USDT")
    }).Concat(new[]
    {
        new InstrumentRef("defillama:chain:Ethereum", "ethereum", "defillama", "Ethereum", InstrumentKind.Chain, "ETH", null, null),
        new InstrumentRef("defillama:chain:Solana", "solana", "defillama", "Solana", InstrumentKind.Chain, "SOL", null, null)
    }).ToArray();
}

public sealed class ProviderRow
{
    public string Id { get; set; } = "";
    public string ApprovalStatus { get; set; } = "Unresolved";
}

public sealed class PayloadRow
{
    public string Id { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string RequestPath { get; set; } = "";
    public string MappingVersion { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
    public DateTimeOffset WindowStartUtc { get; set; }
    public DateTimeOffset WindowEndUtc { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}

public sealed class ObservationRow
{
    public string InstrumentId { get; set; } = "";
    public ObservationKind Kind { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; }
    public int PeriodSeconds { get; set; }
    public string Unit { get; set; } = "";
    public string? QuoteUnit { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? Close { get; set; }
    public decimal? Volume { get; set; }
    public decimal? QuoteVolume { get; set; }
    public decimal? Value { get; set; }
    public string PayloadId { get; set; } = "";
    public DateTimeOffset IngestedAtUtc { get; set; }

    public Observation ToObservation() => new(InstrumentId, Kind, EventTimeUtc, PeriodSeconds, Unit,
        QuoteUnit, Open, High, Low, Close, Volume, QuoteVolume, Value);

    public static ObservationRow From(Observation o, string payloadId, DateTimeOffset ingestedAt) => new()
    {
        InstrumentId = o.InstrumentId, Kind = o.Kind, EventTimeUtc = o.EventTimeUtc,
        PeriodSeconds = o.PeriodSeconds, Unit = o.Unit, QuoteUnit = o.QuoteUnit,
        Open = o.Open, High = o.High, Low = o.Low, Close = o.Close, Volume = o.Volume,
        QuoteVolume = o.QuoteVolume, Value = o.Value, PayloadId = payloadId, IngestedAtUtc = ingestedAt
    };
}

public sealed class QuarantineRow
{
    public string Id { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public string Code { get; set; } = "";
    public DateTimeOffset WindowStartUtc { get; set; }
    public DateTimeOffset WindowEndUtc { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
