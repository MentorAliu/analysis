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

M2's `scripts/verify-m2.mjs` remains offline: loopback fixtures, in-memory private
transport policy tests and a disposable PostgreSQL database. It covers explicit
private-use command parsing, host/path allowlists, request/retry budgets, permanent
provider failure, paced/in-flight cancellation and SIGTERM while one-shot database
I/O is blocked, before any provider transport is constructed.

The separately invoked `scripts/verify-m2-private.mjs --private-use --country XK`
performs authorized public data ingestion into its own private database. It checks
11 data series, exact decimals/units/UTC and raw-byte SHA-256 lineage, no duplicate
observations on an identical-window run, and persistence after recreation. New
response metadata can add provenance payloads without changing existing facts.
It retains its own collected-data volume/local configuration and stops its
containers; it never runs against a shared database or benchmarks provider
availability. Live payloads remain private and are not committed as fixtures.

M3 adds the package-free `Analysis.ScoringChecks` executable. Independent examples
and vectors cover decimal rounding/square root, feature states, exact weights,
confidence non-complements, context isolation, history/units/cutoffs and conflicts.
`scripts/verify-m3.mjs` tests populated-M2 upgrades, disposable rollback,
concurrency, sealed children, UPDATE/DELETE/TRUNCATE rejection, invalid lineage,
actual worker SIGTERM, replay, PostgreSQL recreation and Redis independence.
No numeric/test-framework dependency was introduced. Production images exclude
both M2 and M3 check assemblies and fixtures.

The separately authorized `scripts/verify-m3-private.mjs` acquires one seven-day
batch under existing M2 limits, then requires 75 ready scores and all applicable
features usable over 25 hours. Repeated scoring/replay uses only the internal
database network. A durable claim prevents automatic batch retries; missing
history cannot be hidden by changed thresholds, providers or windows. Reports
separate failures and unavailable gates and retain private data/configuration.
M1/M2 regressions remain required. Hash-identical frontend inputs need no browser
suite rerun for this backend-only milestone.

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

**Implemented M4:** package-free `Analysis.RankingsChecks` exercises the real
Kestrel endpoint/OpenAPI with a synthetic reader, exact decimal/UTC mapping,
ordering/readiness, request rejection before reads, sanitized errors and actual
HTTP cancellation. `scripts/verify-m4.mjs` owns a fresh PostgreSQL database and
checks read-only Repeatable Read, exact/latest/model isolation, concurrent
publication, rejected corrupt snapshots, schema refusal, database cancellation,
Redis independence and unchanged complete database hashes. It compares the
running API/OpenAPI with the committed schema and regenerated frontend files,
checks host/origin/loopback restrictions, and verifies Production default denial.
Never run private acquisition scripts or mount retained volumes for M4 checks.

### Frontend tests

Follow current official React, Vite, TanStack Router, TanStack Query, Zod, and shadcn/ui testing guidance.

**Implemented (2026-09-06):** Vitest 5.0.0 with React Testing Library 16.3.3,
DOM Testing Library 10.4.1, jest-dom 7.0.1 and jsdom 30.0.1 tests public
configuration and the shell with isolated memory histories. Playwright 1.63.0
tests the production bundle in Chromium, Firefox, WebKit and a narrow Chromium
viewport, including navigation/history, reload, missing routes and keyboard
skip-link focus. Fail on runtime errors, external requests and unintended API
requests; allow only explicitly mocked rankings reads. Exact compatibility, image pins and verification
are recorded in the active execution plan; commands are in the root README.

The frontend foundation also tests shared typed Router/Query context, cache
isolation and direct Zod 4 search validation on test-only routes (including rejection
before loader I/O and reuse of fresh Query data). Table v9 tests use nonfinancial
notes to verify semantic rendering, custom cells, empty states, canonical row
identity across replacement/reordering, and external sorting/reset ownership.
No test route or fixture is imported by the production application.

Group unit tests by owner: application/config/query fixtures in `tests/unit/app`,
workspace behavior in `tests/unit/features/workspace`, and reusable components in
`tests/unit/components`. Test setup stays shared. Use `queryOptions` factories even
in fixtures and derive cache keys from their options; strict Query lint applies to
tests as well as source. Application factories preserve global Query defaults.
Document test-only immutable-cache policies beside fixture options rather than
changing application defaults. Regression checks verify untouched defaults and
stable `select` projections: consumers see only their view, complete records remain
cached, unrelated cache changes retain the selected result's identity, and relevant
changes reach the consumer. No financial fixture or production request is needed.

Keep all four production Playwright projects. A separate development Chromium
configuration uses port 4174 to open the actual Query/Router inspectors and verify
route updates; production tests check their absence. Build-time module inspection
rejects shipped devtools, test or CLI code. Both browser suites own their servers,
use zero retries and can run non-root in an isolated Docker container without
external networking. jsdom stubs unsupported scrolling and ResizeObserver; keyboard focus and
viewport behavior are verified in real browser engines. Full accessibility audits,
coverage thresholds and CI setup are not established by this foundation.

M4 adds `tests/unit/features/rankings` using a test-only response exported by the
backend checks. It verifies generated success/problem validation, exact strings,
integer types, rejected malformed responses, cache identity/full response,
untouched Query defaults and actual Fetch signal cancellation. The frontend image
build runs `api:check` before lint/unit/typecheck/production build; synthetic data
never enters the rendered application.

M5 adds a generated-contract dashboard test suite. `tests/support/rankings.ts`
derives synthetic browser responses from the M4 fixture and rejects every
unmocked API or external request. Unit tests cover strict string URL boundaries,
calendar/future validation, exact BigInt rounding/comparison, request/response
coherence, cancellation and unchanged global Query defaults. Browser projects
exercise history/reload, scalar-looking IDs, zero-request invalid inputs, manual
refresh, retained-data failures, 403 suppression, absent batches, offline pause/
cancel/resume, delayed abandoned requests, sorting, details and focus restoration.
Complete, partial, not-ready, missing and inapplicable states remain distinct.

Run `npm run test:e2e:run` after the production build. Axe wrapper/engine 4.13.0
scan seven states for applicable A/AA rules; inspect attached incomplete findings.
Keyboard workflows, 44px visible controls, 320px reflow with 200% text plus WCAG
spacing, forced colors, reduced motion and touch emulation supplement scans.
Snapshots use en-GB, UTC, a fixed clock, light scheme, DPR 1 and pinned Linux
Chromium at 1440x1000, 390x844 and 320x800, including both table ends and open
provenance. Review candidates before accepting them. Native Windows Narrator
with Edge, actual 400% browser zoom, physical touch-device checks and human timed/
comprehension tasks remain separate coverage. The user-approved M5 acceptance
revision of 2026-09-06 defers these checks for private single-user use, after the
agent-led practical workflow review. Record them as deferred, not passed; neither
automation nor agent review substitutes for human usability evidence or establishes
full accessibility conformance. The active plan records the bounded acceptance.

`npm run test:performance` serves production assets and real loopback HTTP with
200ms synthetic API delay. Use the pinned Playwright image, network isolation,
four CPUs/4GiB, one worker, 20 cold navigations per profile (latest/exact alternated)
and 20 repetitions per warm action. CDP shapes CPU/network, browser performance
observers capture LCP/CLS/long tasks and resource sizes, and timeline traces remain
local. Interaction timing starts at the browser event and ends after the required
DOM update has had a paint opportunity; Playwright actionability waits are excluded.
Compare gzip transfers to the unchanged M4 production image baseline. Record raw
results, failures, environment and unavailable gates in the active plan. This
protocol does not measure field INP or prove WCAG conformance.

Run the existing isolated `scripts/verify-m4.mjs` for actual API/OpenAPI
compatibility; its disposable-data ownership and cleanup rules still apply.
Do not run acquisition verifiers as dashboard checks. Preserve all generated
contracts, backend code, model manifests, migrations, Compose and image pins.

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

- Whether to adopt a .NET test framework later: current backend checks are package-free
  executable projects under the pinned .NET SDK, with isolated M1/M2 integration verifiers.
- Coverage gates, if any.
- How production-like the first CI runners are (Docker-in-Docker vs hosted testcontainers).
