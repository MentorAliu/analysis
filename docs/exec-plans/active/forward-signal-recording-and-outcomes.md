# Roadmap 1A — Forward signal recording and outcome collection

**Status:** Implemented on 2026-09-07. All specified automated acceptance gates
passed, including Linux container/image/SIGTERM checks and M1–M4 Compose
regressions. Live activation remains inactive.
Baseline: clean checkout `435e83da87f67bf5f22dd8273ad0491a68fd92c1`.
The preceding conversational plan is approved by the user's “Implement plan”.

## Scope and frozen decisions

Manual bounded worker commands only. No live acquisition is authorized by this
implementation run. Tests use synthetic data, loopback providers and disposable
databases. Do not open retained databases. Preserve BTC/ETH/SOL, slice1-v1 source
and manifest hashes, existing model labels, private access gates, M5 manual refresh,
dependency/image pins and the modular monolith. M1–M5 acceptance is historical
evidence, not new test evidence. Narrator, native 400% zoom, physical touch and human
usability remain deferred. No MFE/MAE, 1B, benchmark UI, thesis monitor, alerts,
trading, continuous acquisition, deployment, commits or pushes.

Issuance is atomic sealing of a private original ranking, not customer delivery.
T is the explicit current UTC hour; K is the database input-snapshot cutoff;
creation time belongs to the scoring bundle; I is the database seal timestamp
after locks, conditional on commit. T <= K <= creation <= I <= T+15min.
The command cannot supply K/I or a batch id. New scoring bundle, issuance, three
items and twelve outcome targets commit together. Existing standalone research
batches cannot be issued. Duplicate model/T returns the original issuance even
after freshness expires. No historical issuance or relabelling. Underlying M3
records retain research-reconstruction; the separate ledger uses
forward-issued-ranking. Complete/partial scores meeting M3 readiness rank by exact
composite descending then canonical id ordinal ascending. Retain negative and
all-not-ready output, full universe, eligible set, quality and original order.

Freeze `forward-1a-v1`: model/input/feature/score/payload/mapping/conflict links and
hashes, universe and eligibility, original rows, benchmark ordering, planned price
keys and horizons. Store canonical documents plus relational links. Its separate
implementation resources end in .source and must not alter M3's .cs source digest.

Outcomes: Binance spot BTCUSDT/ETHUSDT/SOLUSDT 1h UTC klines. E is the first UTC hour
strictly after I; P0 is the close ending E. M=E+H; P1 is the close ending M. Horizons
are 1h,4h,24h,168h; 24h is primary descriptive horizon. Return=round18(P1/P0-1),
ToEven, checked decimal, fraction units; prices retain USDT and original precision.
Disclose E-I as `referenceDelayMilliseconds`. Require all H+1 candles including reference. Before M pending; at/after
M complete only with full valid undisputed history. No interpolation/substitution,
USD parity, execution price, fees, slippage, funding, compounding or direction flip.

Benchmarks: BTC return over identical E–M; and top asset by
round18((1+asset24hReturn)/(1+btc24hReturn)-1), frozen from M3 feature 5 over eligible
assets, descending then ordinal id. Unavailable inputs remain unavailable forever
for that issuance. Report per-original-rank arithmetic mean returns and model-top1,
BTC, relative-strength-top1 means/differences. Paired samples require original RS
ordering and all three complete undisputed paths. Partition by model/hash,
methodology, horizon, eligible set and original quality composition. Include all
records in an explicit issued-time range, equal issuance weights, null empty means,
sample/coverage/exclusion counts. No predictive/probability claims or independent
sample assumptions for overlapping horizons.

Inspection exposes paired counts, individually complete counts for every original
rank, complete/conflicted/incomplete/pending/unassessed asset counts and exclusion
reasons. Means use exact scaled integer reduction followed by round18 ToEven.
If a mean cannot fit the existing 28-digit/18-place decimal contract it is null
with a named `numeric-range` aggregation issue; its denominator is preserved.
Unissued-hour counts include only fully elapsed T..T+15min issuance windows wholly
inside the requested range, so partial boundaries and still-open windows are not
reported as missed issues.

## Jobs, data and persistence

Worker commands: collect-forward-inputs-once, issue-forward-once,
collect-outcome-prices-once, measure-outcomes-once, inspect-forward-records.
Strict parsing, UTC milliseconds, decimal strings, cancellation, run/correlation ids.
Measurement pages at most 24 issuances, ordered by I/id with continuation cursor.
Default worker and API do not acquire or issue. No HTTP/OpenAPI/client changes;
share Application ranking order with existing API mapper.

Input collection: explicit current T and private-use/country XK, last 120h;
candles [T-120h,T), scalar events [T-120h,T+1ms), with the latter past at execution.
Include exact OI at T without unfinished candles. Existing adapters and fixed
origins/budgets: 128 attempts/provider/run, 1s pacing, 3 HTTP attempts, 32 pages,
4MiB response, 5min run deadline. Outcome collection is Binance-only, explicit
closed hourly windows <=7d and only intervals needed by stored issuances. The 169
candles for a 7d result may need multiple bounded invocations. Live activation
requires separate authorization of country/database/providers/windows/invocations
and current rights/access review; no proxy, redirect or regional workaround.

Forward-only observation persistence keeps first accepted facts and rejected
candidate payload/value evidence plus precise quarantine; unrelated valid facts
commit. Preserve legacy M2 semantics. Assessments append pending, incomplete,
complete or conflicted states with cutoff, input hashes, missing keys and conflict
links. Late first facts can complete a prior incomplete outcome. Duplicate evidence
is a no-op. Completed numeric results/prices never change; subsequent conflict
appends a disputed assessment. Conflicts do not expire; missing outcomes remain
retryable with no terminal expiry. Replay frozen evidence, never current history.
Each rejected revision retains its own ID, original fact, candidate value,
payload/mapping/hash and detection time. Another candidate for an already disputed
candle appends another distinct evidence state; older assessments retain only the
revision evidence available at their original cutoff.
Publication skips stale captures. For equal millisecond cutoffs, a candidate must
include the latest assessment's facts, conflict IDs and revision IDs before it may
advance the ledger; this prevents a delayed snapshot from clearing a known dispute.

Additive EF migration Roadmap1AForwardSignalsOutcomes owns ForwardMethodologies,
ForwardIssuances/Items, ForwardOutcomeTargets, ForwardOutcomeAssessments/Inputs,
ForwardObservationConflicts and ForwardRunEvents. Restrictive exact lineage FKs,
unique logical identities, clock/state/precision checks, immutable row/truncate
and sealed-child guards. Capture read-only Repeatable Read, calculate outside
publication transaction; fixed-order locks, 30s publication timeout, atomic seal.
Outcome writes serialize per issuance. Redis is unnecessary. Explicit --migrate;
Down/reapply only disposable DBs, retained recovery by backup/forward repair.

## Sequence and inventory

1. Freeze methodology and independent arithmetic/time/order vectors.
2. Domain SignalsOutcomes contracts/calculators; Application recording, collection,
   outcome jobs, record reads and shared RankingOrder.
3. Infrastructure ForwardCommand, ForwardSchema/Store/ObservationStore/RecordReader;
   refactor ScoringStore transaction/capture helpers, configure ResearchDbContext
   and Registration; generate migration/designer/snapshot using pinned EF tool.
4. Worker ForwardOperation and Program dispatch; existing API RankingTransport
   calls shared order without contract change.
5. Add Analysis.ForwardChecks project (Program, Check, Synthetic, UnitChecks,
   DatabaseChecks, CollectionChecks, csproj and lock), scripts/verify-1a.mjs;
   wire solution/Dockerfile and update M3's fixed migration-count assertion.
6. Run checks below and update README, architecture, roadmap, domain/pipeline,
   data-sources/testing documents and this plan with actual evidence.

No frontend, old migration, scoring source/manifest or dependency pin edits.

### Final file inventory

| Owner | Files added or changed |
| --- | --- |
| Domain | `backend/src/Analysis.Domain/SignalsOutcomes/{ForwardContracts,ForwardMethodology,OutcomeCalculator,BenchmarkCalculator}.cs`; `Manifests/forward-1a-v1.json`; `Analysis.Domain.csproj` resource registration |
| Application | `backend/src/Analysis.Application/{ForwardRecordingJobs,ForwardOutcomeJobs,ForwardRecordReads,ForwardCollection,RankingOrder}.cs` |
| Infrastructure | `backend/src/Analysis.Infrastructure/ForwardCommand.cs`; `Persistence/{ForwardSchema,ForwardStore,ForwardObservationStore,ForwardRecordReader}.cs`; small `ScoringStore`, `ResearchDbContext`, `Registration` changes |
| EF | `20260907092004_Roadmap1AForwardSignalsOutcomes.cs`, its designer and `ResearchDbContextModelSnapshot.cs` under the existing migration directory |
| Hosts | `backend/src/Analysis.Worker/ForwardOperation.cs`, worker `Program.cs`; API `Rankings/RankingTransport.cs` uses the shared order without transport changes |
| Checks | `backend/tests/Analysis.ForwardChecks/{Program,Check,Synthetic,UnitChecks,CollectionChecks,DatabaseChecks}.cs`, project and lockfile; existing ScoringChecks migration-count assertion follows the migration assembly |
| Build/verification | `backend/Analysis.slnx`, `backend/Dockerfile`, `scripts/verify-1a.mjs` |
| Documentation | This plan, README, architecture, roadmap, domain model, pipeline, data sources and testing strategy |

EF owns all schema SQL, including immutable/truncate guards, sealed-child and
deferred completeness checks, exact timestamps/numeric bounds and the unique
first-completed-assessment index. No schema is applied at ordinary startup.

## Acceptance gates

- Issuance time boundaries, historical refusal, duplicate reuse, negative/empty
  eligibility, three rows/twelve targets, exact M4 order and unchanged M3 hashes.
- Independent positive/negative/zero/repeating/ToEven/overflow return vectors,
  maturity boundaries, H+1 coverage, units/identity, no future candles/substitution.
- Missing-to-complete, duplicate no-op, later conflicts preserve old numeric result,
  independent valid collection commits and frozen benchmark ordering.
- Benchmark matched samples, grouping, denominators, unavailable/empty aggregates.
- Four concurrent issuance/measurement writers, cancellation/SIGTERM, rollback,
  uncertain commit recovery, immutability/sealed-child and restrictive lineage.
- Empty/populated upgrade, drift, disposable Down/reapply, unchanged old rows,
  database recreation, frozen replay and Redis independence.
- Loopback collection at exact scalar T, time/identity/budget/denial/cancellation
  boundaries; API/default worker never acquires or issues.
- Locked builds, offline M1–M4 verifiers, OpenAPI/generated drift, targeted M5
  refresh/request regressions. Exclude fixtures/check assemblies from production.
- Synthetic all-horizon verification with test clocks; no production clock override.
  Disposable database tests may replace only their own SQL clock function.

Acceptance requires the applicable offline gates. Container results must not be
inferred from Windows checks or historical M1–M5 evidence. Both Windows and
container verification have now passed as recorded below. Live operation remains
separate and inactive. Record passes/failures/skips/unavailable separately.

## Official sources

Checked during planning and refreshed as needed during implementation:
[EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions),
[EF migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying),
[PostgreSQL clocks](https://www.postgresql.org/docs/18/functions-datetime.html),
[locks](https://www.postgresql.org/docs/18/explicit-locking.html),
[triggers](https://www.postgresql.org/docs/18/sql-createtrigger.html),
[transaction limits](https://www.postgresql.org/docs/18/runtime-config-client.html),
[Npgsql UTC](https://www.npgsql.org/doc/types/datetime.html),
[Binance klines](https://developers.binance.com/en/docs/catalog/core-trading-spot-trading/api/rest-api/market),
[market-data-only](https://developers.binance.com/en/docs/products/spot/faqs/market_data_only),
[Bybit OI](https://bybit-exchange.github.io/docs/v5/market/open-interest),
[funding](https://bybit-exchange.github.io/docs/v5/market/history-fund-rate),
[DeFiLlama schema](https://github.com/DefiLlama/api-docs/blob/main/defillama-openapi-free.json).

## Implementation evidence

**Initial implementation phase, 2026-09-07, before Docker resumed:** HEAD and local origin/main both matched
`435e83da87f67bf5f22dd8273ad0491a68fd92c1`, with a clean working tree. The older
execution plan's M1–M5 results remain historical evidence and were not rewritten.

| Check | Initial implementation-phase result |
| --- | --- |
| Exact SDK 10.0.400 locked solution restore and Release build | Passed, zero warnings/errors; no existing dependency pin or lockfile changes |
| EF 10.0.11 tool/model drift | Passed; migration generated without opening a retained database |
| Existing M1–M4 executable offline checks | Passed: operational checks, M2 transport/mapping suites, M3 101 assertions, M4 146 assertions. Their Docker integration verifiers were not rerun |
| New deterministic and loopback checks | 191 assertions passed: clocks, arithmetic, readiness/order, horizon paths, units, frozen benchmarks, denominators, range failures, exact scalar T, denial isolation and HTTP cancellation |
| New PostgreSQL integration suite | 69 assertions passed on disposable PostgreSQL 18.6: empty/populated upgrade, Down/reapply, four issuance writers, four measurement writers, incomplete/pending to complete, unfavorable returns, two revisions to one candle, idempotency, immutable/sealed writes, actual database cancellation, failure after scoring publication and rollback, 26-issuance cursor/whole-range inspection, deterministic equal-cutoff stale-capture race |
| Actual worker/API, database restart, Redis absent | Passed: immutable read-only inspection/history, original duplicate returned, exact persisted snapshot after restart, API/default worker no data writes, original M3 record label |
| Actual OpenAPI and generated Fetch/types/Zod | Passed without contract drift or source edits |
| Targeted M5 tests | 41 passed across dashboard, transport, application and config; existing manual-refresh/request behavior preserved |
| Linux image, container recreation/security and POSIX SIGTERM | Initially unavailable because the Docker Desktop Linux engine pipe was absent; subsequently passed in the resumed verification below |
| Live provider acquisition / retained DB migration / scheduling | Not performed; not authorized by this implementation session |
| Deferred M5 human/accessibility checks | Not reopened; original reduced acceptance scope preserved |

The final portable-runtime report and individual command logs are under
`.artifacts/analysis-1a-check-987bd60e9e6a/`. This final run includes first-query
snapshot-cutoff ordering and the equal-cutoff concurrency guard. Its report has no failed checks and records
the unavailable Docker gates separately. These artifacts contain synthetic checks,
not forward market observations or benchmark performance. Temporary cluster data
and passwords were removed after every completed run. One initial startup-handle
failure left a stopped test cluster; its exact task-owned directory was separately
verified and removed. Retained project data was never opened.
Final `git diff --check`, verifier syntax and protected-file checks passed. Frontend,
generated contracts, M3 numerical sources/manifest, existing pins/lockfiles and the
historical first-ranking execution plan have no changes. HEAD remains at the
handoff commit; no commit or push was made.
An earlier attempt of `scripts/verify-1a.mjs` without `--local` stopped at its first
Docker inventory command because the Linux engine pipe was absent; no test
container or volume was created. That infrastructure failure is recorded in
`.artifacts/analysis-1a-check-c2abbf56e7cd/report.json`. It does not constitute a
container test pass or a financial-data test failure.

The host initially lacked .NET. The official SDK archive was downloaded into
ignored task artifacts and verified against Microsoft's release-metadata SHA512.
Docker Desktop could not start its Linux engine: its log reported an inaccessible
`sailor-ingest.sock`, and its named engine pipe remained absent. No Docker reset,
volume deletion or retained-database workaround was used. Portable PostgreSQL 18.6
came from the [EDB archive linked by PostgreSQL](https://www.postgresql.org/download/windows/)
([official archive page](https://www.enterprisedb.com/download-postgresql-binaries),
file ID 1260488). The downloaded archive's local SHA256 fingerprint is
`59f8ce701c63c2ed623c665a5e51b3ef6f2e37ccf837b68ffeed0742d0ae6abd`;
this records the bytes used, not an independent publisher checksum comparison.

Development failures were corrected before the passing runs: EF's configuration
flag, the portable pg_ctl inherited-pipe handling, worker output-envelope/argument
assertions, and an immutability test that initially targeted an empty audit table.
The expanded tests also verified independent revision references and explicit
aggregate denominators. No failed financial result was removed from a ledger.

Frozen digests confirmed by executable checks:

- M3 manifest: `acef235e40c75ed4b4aa3f430dda949c9163afdd92aab73c49a7143ee5137eb1`.
- M3 calculator: `d57997b39e15e37a40d79ed52e5fe36dec48b2f08e0506b16988a599a3747656`.
- 1A methodology: `1c14d694297fcbdf311e8070a4fa209bd7647dfbf4bb5e9464dc06cdfeaf29ee`.
- 1A domain implementation: `f5ec5b3a0651de99c82e6ba600c62536da567628576a9c7e8061490ca2d4d3c7`.

### Resumed Docker verification — 2026-09-07

After the user reported Docker available, the engine was verified as Docker Desktop
4.89.0 / Engine 29.7.2, Linux amd64. Pinned Node 24.20.0 ran the following existing
verifiers sequentially. Each returned exit 0, recorded no failed checks and verified
removal of its own containers, networks and disposable database volume. No
application or verifier code changes were required during this continuation.

| Verifier | Result and report |
| --- | --- |
| `scripts/verify-1a.mjs` | Passed locked Linux builds, offline executable checks, all 69 PostgreSQL assertions, actual worker inspection/duplicate recovery, Redis outage, PostgreSQL container recreation, actual SIGTERM with exit 130/no partial persistence, and non-root worker fixture/assembly separation. `.artifacts/analysis-1a-check-2babcfecb684/report.json` |
| `scripts/verify-m1.mjs` | Passed all nine operational groups: health/progress, OpenAPI/proxy, problem details, isolation/non-root users, dependency failure/recovery, persistence through recreation and shutdown/log correlation. `.artifacts/analysis-m1-check-45ac25d37b89.json` |
| `scripts/verify-m2.mjs` | Passed offline mappings and database precision/lineage/concurrency/quarantine, explicit migration reapplication, recreation, provider-access refusal and actual blocked-I/O SIGTERM before any provider access. `.artifacts/analysis-m2-check-506a1a1e7b98.json` |
| `scripts/verify-m3.mjs` | Passed database-check mode with 151 assertions, frozen scoring hashes/replay, sealed snapshots, actual command/refusal behavior, Redis independence, blocked-write SIGTERM and recreation. `.artifacts/analysis-m3-check-c0de5cf4c846.json` |
| `scripts/verify-m4.mjs` | Passed actual API/OpenAPI/generated transport drift checks, database read integrity/cancellation, exact decimals/UTC, host/origin boundaries, Redis independence, Production default denial and non-root API fixture separation. `.artifacts/analysis-m4-check-e3a35ec65b61.json` |

The 1A Linux run confirmed all four frozen digests above. The earlier 191
deterministic/loopback assertions also ran in its pinned backend build. Earlier
M5 evidence remains the 41 targeted tests from the implementation phase; unchanged
frontend sources did not require reopening deferred human/accessibility checks.
The prior M1–M5 execution plan remains unchanged. These are synthetic/offline
correctness results, not observations of forward predictive performance.

There are no outstanding automated acceptance gates for this bounded 1A change.
Neither private acquisition verifier was run. No live provider acquisition,
retained-database operation, continuous scheduling, deployment, commit or push was
performed. Passing tests does not activate live operation.

### Separate manual activation

For a separately authorized manual live session, specify the database/project,
country, providers, time windows and bounded invocations; review current provider
rights/access, approve the additive migration, collect current inputs, issue within
the freshness window, later collect the needed closed candles, measure with cursor
continuations and inspect all outcome states. Preserve backups and use a forward
repair for retained-data recovery; disposable Down tests are not a retained-data
recovery procedure. Continuous collection needs a later explicit authorization.
