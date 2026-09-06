# Data sources

**Status:** Requirements for capabilities. Product/live vendor approval is unresolved. M2's offline technical candidates and licensing/access blockers are recorded in the [active plan](../exec-plans/active/first-ranking-vertical-slice.md#m2--catalog-and-adapter).

Do not treat names mentioned in conversation or common industry lists as selected vendors. Do not invent API endpoints, history depth, or rate limits. Confirm capabilities against the provider’s official documentation during evaluation.

Abstraction rules: [../design/domain-model.md](../design/domain-model.md), [../design/data-pipeline.md](../design/data-pipeline.md). Architecture: [../../ARCHITECTURE.md](../../ARCHITECTURE.md).

## Provider abstraction

**Requirement:**

- All external access goes through adapters behind application ports.
- Domain code depends on canonical observation kinds, not vendor SDKs.
- One canonical asset may have many provider instrument refs (spot, perpetual, indexer address, coingecko-style id, and so on).
- Switching a vendor should not require rewriting feature or scoring formulas, only mapping and quality tests.
- License, redistribution, and display terms are part of vendor fit. A technically complete API that cannot be shown in the product is not a fit.

Each adapter owns: authentication, pagination, rate-limit behavior, DTO mapping, and schema-drift detection hooks.

## Evaluation criteria

For every candidate provider, record evidence (link to official docs + a dated note). Until then, the item stays **Unresolved**.

| Criterion | Why it matters |
| --- | --- |
| Coverage | Assets, venues, and markets needed for Stage A vs Stage B |
| History | Backfill length for features, scoring replay, and outcomes |
| Granularity | Candle sizes, snapshot vs tick, funding interval |
| Latency and freshness | Whether cadences in the pipeline can be met |
| Rate limits and cost | Whether Stage A universe scans are affordable |
| Units and identifiers | Symbol ambiguity, contract specs, decimal scale |
| Schema stability | Breaking changes and versioning policy |
| Licensing | Storage, derived metrics, and UI display rights |
| Reliability | SLA, status history, documented error model |

Multiple providers per capability class are acceptable. Silent blending of conflicting numbers is not. See scoring missing-data rules in [../design/scoring-model.md](../design/scoring-model.md).

## Capability requirements by class

### Market data

Needed to support price structure, relative strength, Stage A filters, and outcome returns.

- OHLCV candles at multiple timeframes
- Trades and/or volume
- Last/mark/index price as documented by the venue or vendor
- Market cap, circulating supply if used for Stage A (may overlap tokenomics)
- Spread, depth proxies, and venue coverage for eligibility
- BTC dominance, ETH/BTC, and a defined alt-basket or equivalent for regime

**Unresolved:** venue vs aggregator; which series is canonical for returns.

### Derivatives

- Perpetual (and liquid dated) open interest
- OI change and OI relative to market cap (market cap may come from another class)
- Funding rates
- Futures basis
- Futures vs spot volume/activity
- Liquidations and, where available, liquidation clusters
- Long/short positioning metrics where the venue publishes them
- Options metrics **only** where the market is liquid enough to trust

**Unresolved:** which venues’ perpetual is canonical per asset; how to treat multi-venue OI.

### Order flow

- Spot volume
- Spot CVD (needs trade-side or reliable aggressor inference)
- CVD divergences (derived; needs history)
- Order-book depth snapshots
- Bid/ask imbalance
- Liquidity gaps
- Slippage estimates (requires depth + a documented trade-size assumption)

Many “CVD” products are vendor-specific. Treat vendor CVD as a provider metric until the formula is reproduced in-house.

### On-chain / capital flows

- Exchange inflows/outflows
- Exchange reserves and netflows
- Large-holder accumulation/distribution
- Labeled wallet classes: insider, VC, foundation, treasury — only if labeling quality is acceptable
- Post-unlock token movements (depends on unlock events + transfers)
- Holder concentration
- Cost-basis / realized-cap style metrics where useful and licensed

Label quality is part of capability, not a cosmetic extra.

### Tokenomics / unlocks

- Circulating vs total supply
- FDV and the supply definition behind it
- Unlock schedules and observed unlocks
- Emissions, inflation, staking issuance
- Burns, buybacks, treasury accumulation

Supply definitions differ across vendors. The domain must record **which definition** was used.

### Protocol fundamentals

- TVL
- Fees and revenue (define what “revenue” means per protocol)
- Users and transactions
- DEX and perpetual protocol activity
- Ecosystem/integration/adoption and development-progress series **only** if they are structured measurements, not blog sentiment

Not every asset has protocol fundamentals (BTC vs ETH vs SOL). Applicability is required.

### Macro

- Broad financial conditions relevant to crypto risk appetite (rates, USD, risk indices, or a documented subset)
- Stablecoin market-cap or liquidity proxies for regime

**Unresolved:** which series, which vendor, what revision policy (macro series get revised).

### News / catalysts

- Dated events: upgrades, listings, governance, partnerships, institutional items, regulatory/legal, security incidents
- Source identity and event time
- Enough text or structured fields to attach to an asset without using the LLM as a score

**Future:** LLM extraction into `IntelligenceEvent`. **Requirement:** extraction is candidate generation, not automatic truth.

## Decisions requiring later validation

These are **Unresolved** until an exec plan records evidence:

1. Primary market-data provider and canonical candle/return series.
2. Primary derivatives venue set.
3. Whether order-book/CVD is in-house from raw trades/books or licensed.
4. On-chain and labeling vendor vs self-indexed data.
5. Token unlock calendar source.
6. Fundamentals vendor vs protocol-native subgraphs/APIs.
7. Macro series source.
8. News/catalyst vendor vs public RSS plus manual curation.
9. Redis/Postgres hosting does not replace these market-data choices.

The first vertical slice may use a **temporary** provider subset if official docs confirm the needed fields for BTC, ETH, and SOL. Temporary is not permanent. Record the subset in the exec plan.

## What not to do

- Do not hard-code a vendor DTO as `Observation`.
- Do not assume a vendor has tick-level history because it has a live websocket.
- Do not scrape as a substitute for a licensed API unless a later exec plan explicitly accepts that legal and reliability risk.
