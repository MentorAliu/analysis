# First ranking vertical slice

**Status:** Active execution plan. M1 was implemented and verified on 2026-08-29; M2 has not started. This plan proves the end-to-end architecture before the full indicator catalog.

Read first: [AGENTS.md](../../../AGENTS.md), [ARCHITECTURE.md](../../../ARCHITECTURE.md), [../../product/product-spec.md](../../product/product-spec.md), [../../design/domain-model.md](../../design/domain-model.md), [../../design/data-pipeline.md](../../design/data-pipeline.md), [../../design/scoring-model.md](../../design/scoring-model.md), [../../engineering/data-sources.md](../../engineering/data-sources.md), [../../engineering/testing-strategy.md](../../engineering/testing-strategy.md).

Use official documentation for React, Vite, TanStack Router, TanStack Query, Zod, shadcn/ui, ASP.NET Core, PostgreSQL, Redis, and Docker when scaffolding. Pin supported stable/LTS versions at implementation time.

## Goal

Ship the smallest path that a later agent can extend:

External provider → adapter → normalized observations → ~15–25 features → versioned deterministic scores → PostgreSQL → `GET /rankings` → basic dashboard.

Universe: **BTC, ETH, SOL** only.

## Non-goals for this slice

- Broad Stage A scanner over the market
- Watchlists, alerts, auth (unless a stub blocks local use; prefer no auth)
- LLM explanations
- Outcome/MFE/MAE jobs
- Order-book/CVD/on-chain/whale labeling
- Full ten-family scoring
- Microservices, Kafka, Kubernetes
- Trading or custody
- Final provider vendor lock-in
- Probability copy in the UI

## Success criteria

The slice is done when all of the following are true:

1. Docker Compose brings up frontend, API, worker, PostgreSQL, and Redis on Docker Desktop.
2. A worker run ingests provider-validated market and derivatives data for BTC, ETH, and SOL, plus at least one meaningful documented protocol-fundamental series for ETH and SOL.
3. Features and append-only score snapshots persist with immutable `ScoringModelVersion`, exact feature-input lineage, and UTC as-of times.
4. Re-running the worker does not duplicate scores for the same as-of + model version.
5. `GET /rankings` returns the three assets with composite, category, confidence, data-quality, model version, and freshness.
6. The dashboard lists that ranking via TanStack Query and generated OpenAPI types.
7. Golden tests lock scoring for a frozen fixture.
8. UI copy says “composite score” / “confidence”, not probability.

## Provisional feature set (15–25)

**Provisional.** Include a feature only if the chosen provider’s official docs confirm the inputs. If a candidate cannot be sourced, replace it with another documented high-value feature and record the change. If fewer than 15 defensible features remain, revise this plan rather than inventing a proxy or quietly shrinking the acceptance scope.

Target mix (adjust to land inside 15–25):

**Market / price structure**

- Spot close and volume on a documented series
- Simple returns over a short and medium window
- Realized volatility over a documented window
- Relative strength vs BTC (ETH, SOL); BTC’s own RS is omitted or vs a documented USD/stable reference
- Distance from a documented moving-average (confirmation-level, low weight)

**Market regime (small)**

- BTC return/trend feature used as context for alts
- BTC dominance **if** documented by the market-data source; otherwise skip
- ETH/BTC return **if** both legs exist

**Derivatives**

- Perpetual funding (level and change)
- Open interest level and change
- OI versus market cap **if** both units are defined
- Futures basis **if** documented
- Liquidation notional over a window **if** documented

**Fundamentals (applicable only)**

- ETH: one documented usage/fee or TVL-style metric if available
- SOL: one documented usage/fee or TVL-style metric if available
- BTC: no fake protocol TVL; leave fundamentals inapplicable

**Tokenomics (only if trivial and documented)**

- Circulating supply / FDV if the market-data source already provides a definition

Do not add RSI/MACD/Bollinger as composite drivers in this slice. They may appear later as confirmation.

Exact windows, units, and formulas belong in the first `ScoringModelVersion` manifest, not in this plan.

## Provisional scoring

- One immutable model version id, for example `slice1-v1` plus a manifest hash.
- Categories with non-zero weight only if they have features: price structure, derivatives, regime; fundamentals when applicable.
- Document missing/inapplicable/stale behavior per [../../design/scoring-model.md](../../design/scoring-model.md).
- Weights are **provisional heuristics** listed in the manifest. Do not describe them as optimized or predictive.

## Milestones

Implement in order. Do not skip to the dashboard.

### M1 — Repository and Compose skeleton

- `frontend/` Vite + React + TypeScript
- `backend/` ASP.NET Core API + worker in one modular solution
- Compose: frontend, api, worker, postgres, redis
- Health checks, `.env.example` without secrets
- OpenAPI enabled on the API
- Vite development proxy for `/api` (or a narrowly scoped development CORS policy if the proxy is not practical)

M1 verification (2026-08-29):

- [x] Strict frontend build, lint, and tests pass
- [x] Nullable-enabled backend Release build and API/worker tests pass with zero warnings
- [x] `docker compose up --build` starts all five services healthy
- [x] Vite and production `/api` proxies, OpenAPI, dependency-aware health JSON, RFC 9457 problem details, and correlation IDs respond correctly
- [x] Frontend production, API, and worker images build as multi-stage non-root images
- [x] CI runs frontend, backend, and disposable Compose smoke checks with locked dependencies

### M2 — Catalog and adapter

- Canonical assets BTC, ETH, SOL
- ProviderInstrumentRef table/map
- One market-data adapter and one derivatives adapter (same vendor allowed if official docs cover both)
- One fundamentals source that provides a meaningful applicable series for ETH and SOL
- Normalized OHLCV, funding, OI (and other confirmed fields)
- Mapping tests from documented fixtures

### M3 — Features and scores

- Feature jobs for the approved 15–25
- Scoring job writing category, composite, bullish/bearish confidence, data quality
- Idempotent, append-only score persistence with exact feature-snapshot lineage
- Golden vector tests

### M4 — Rankings API

- `GET /rankings` (name may be version-prefixed) returning persisted rows
- Problem details for errors
- Generated frontend client from OpenAPI
- Generated runtime validation where the selected OpenAPI tooling supports it without hand-duplicating DTOs
- Contract test that generated artifacts match the spec

### M5 — Ranking dashboard

- TanStack Router route for rankings
- TanStack Query poll or fetch
- shadcn/ui table: asset, scores, quality, as-of, model version
- No chart library in this slice; selecting one remains **Unresolved**

## Acceptance checks

- [x] A clean `docker compose up --build` starts frontend, API, worker, PostgreSQL, and Redis, and the repository README documents the command
- [x] UTC timestamps in API JSON
- [ ] API precision and unit conventions are documented; exact values use a round-trip-safe wire representation
- [x] Worker logs correlation of run id
- [ ] Three assets appear even if one category is inapplicable
- [ ] Historical score rows retain immutable model and exact feature-input lineage; later runs do not replace prior as-of snapshots
- [ ] Tests from [../../engineering/testing-strategy.md](../../engineering/testing-strategy.md) that apply to M1–M5 pass

## Risks

| Risk | Mitigation |
| --- | --- |
| Provider docs do not support the hoped-for 15–25 features | Cut features; do not fake them |
| Symbol/perp confusion across BTC/ETH/SOL | Instrument map tests before scoring |
| Funding unit mismatch | Contract tests against vendor documentation |
| Scope creep into scanner/alerts/charts | Reject in review unless this plan is revised |
| Next.js or extra services added by habit | Follow [ARCHITECTURE.md](../../../ARCHITECTURE.md) |

## Unresolved (must be decided during M1–M2, then recorded)

- Concrete provider(s) and license
- Exact feature list after doc validation
- Manifest weights
- Canonical candle interval for the slice (for example 1h)
- Charting library (can remain unused in M5)

## Recommended next Codex task

Execute **M2** only: record official provider-capability and licensing evidence, then implement the BTC/ETH/SOL catalog plus adapter-local mappings and contract fixtures. Do not implement features or scoring until the validated M2 inputs are recorded.
