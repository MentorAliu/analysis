# Testing strategy

**Status:** Requirement for what must be tested. Tool versions follow official docs at implementation time.

Financial data fails in units, time, identity, and silent interpolation more often than in business logic. Tests must target those failures.

Related: [../design/data-pipeline.md](../design/data-pipeline.md), [../design/scoring-model.md](../design/scoring-model.md), [../../ARCHITECTURE.md](../../ARCHITECTURE.md).

## Principles

- Deterministic scoring and features get golden tests first.
- Provider adapters get contract tests against fixtures from official documented payloads, then drift checks.
- Integration tests run against disposable PostgreSQL and Redis containers, not a shared laptop database.
- Frontend tests consume the OpenAPI contract (mocked or generated client). Detect client/API drift in CI.
- A test that needs “current mainnet data” is not a unit test.

## Layers

### Unit tests

Pure functions: mapping helpers that do not I/O, feature math, normalization, direction alignment, missing-data policy, problem-details mapping, Zod schemas for search params.

### Provider mapping and contract tests

Given a recorded payload (from vendor docs or a captured sample with license to store):

- Maps to the correct canonical asset and instrument type
- Units and scales match the vendor’s documented meaning
- Unknown fields are ignored; missing required fields fail closed
- Native symbol aliases do not collapse distinct products (spot vs perpetual)

### Fixture tests

Checked-in JSON/CSV fixtures represent known market episodes (normal session, funding print, missing candle, duplicate trade id, stale snapshot). Prefer small fixtures over full-day dumps.

### Feature-calculation tests

- Known input series → known feature values within documented numeric tolerance
- Window edges: incomplete windows yield missing, not a short average, unless the manifest says otherwise
- As-of isolation: computing at T must not read T+1

### Scoring tests

- Golden vectors: frozen features + frozen manifest → frozen category, composite, and confidence scores
- Version identity: changing a weight changes the version and the expected vector
- Inapplicable features do not enter as zero
- Replay: same immutable inputs and versions yield exact decimal-equal results
- Score records reference the exact feature snapshot, not a mutable “latest” query
- Score-change explanations separate model-version changes from input changes

### Integration tests

API + worker + PostgreSQL + Redis in Compose or testcontainers-style lifecycle.

- Job idempotency (run twice, one logical row)
- Rankings read matches last persisted scores
- Health checks
- Cancellation of in-flight HTTP calls

### Migration tests

Schema migrations apply on empty and on a representative snapshot; rollback story documented. Never merge an irreversible data loss without an exec-plan note.

### API tests

- OpenAPI matches implemented endpoints
- Generated client/types and any generated runtime validators match OpenAPI
- RFC 9457 problem details on errors
- Authz when auth exists
- Rankings query params reject invalid filters

### Frontend tests

Follow current official React, Vite, TanStack Router, TanStack Query, Zod, and shadcn/ui testing guidance.

- Router search params round-trip rankings filters
- Query polling/error states
- Components do not invent scores; they render API data
- Component tests for ranking table empty/loading/error/stale

### Data-integrity tests

Scheduled or CI jobs over stored data (can be subset):

- No duplicate (asset, provider, kind, event time) observations
- Freshness SLOs flagged
- Orphan features (no observation lineage)
- Scores without model version

### Historical reproducibility

Replay a stored range with a pinned model version and compare to persisted scores. Any difference is a defect unless the version was a documented bug-fix with a new id.

### Provider schema-drift detection

Detect removed fields, type/scale/unit changes, enum changes, and mapping-invariant failures against live or sandbox payloads. Additive unknown fields should be reported and sampled but need not fail ingestion unless they make the existing mapping ambiguous. Do not wait for NaN scores in production.

## Financial-data risks

Every adapter, feature, and migration review should ask:

| Risk | Symptom | Guard |
| --- | --- | --- |
| Percent vs fraction | Funding `0.01` treated as 1% or 100% | Explicit unit on the observation; tests for both interpretations against vendor docs |
| Timestamp confusion | Bar open vs close; local vs UTC; ms vs s | UTC-only persistence; fixture with known epoch |
| Decimal precision | 1e-8 sizes become 0; USD notionals drift | Precision-safe numeric types; no float goldens |
| Symbol ambiguity | XBT/BTC; perp vs spot; ticker reuse | ProviderInstrumentRef tests |
| Duplicate data | Double volume, double OI | Idempotent upsert keys |
| Stale data | Rankings look live, features are hours old | Freshness flags on scores |
| Missing candles | Returns over a hole | Incomplete-window policy tests |
| Provider discrepancies | Two “prices” disagree | Primary-source rule; no silent average |
| Index vs last vs mark | Outcome returns jump at settlement | Documented series in outcome job tests |
| Quote currency | USDT vs USD vs USDC treated as 1 | Explicit quote asset |

## CI expectations

**Proposed:** one CI pipeline runs unit + fixture + scoring goldens on every change; integration + migration tests on PR; contract/drift jobs on a schedule once live providers exist.

Do not skip scoring goldens because a UI screenshot “looks ranked”.

## Unresolved

- Exact JS/TS test runner and .NET test stack versions (choose from official current docs).
- Coverage gates, if any.
- How production-like the first CI runners are (Docker-in-Docker vs hosted testcontainers).
