# First ranking vertical slice

**Status:** Active execution plan. M1 is implemented and verified (2026-09-05). M2's user-authorized offline scope is implemented and verified; live provider use is blocked on licensing/access validation. M3–M5 are not started. M2's live gate and the vertical slice are not complete.

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

#### M1 implementation checklist (2026-09-05)

- [x] Inspect repository, operating contract, referenced design/product/testing documents, roadmap, stack guidance, and available tools. Preserve the pre-existing untracked stack-guidance document.
- [x] Consult official documentation; resolve and pin compatible supported versions, lockfiles, and image digests; record decisions here.
- [x] Scaffold the minimal frontend and inward-dependent backend hosts, operational endpoints, and cancellation-aware worker lifecycle.
- [x] Wire the five-service Compose environment, private data services, PostgreSQL storage, development proxy, and non-root production images.
- [x] Document reproducible commands and verify builds, browser loading, health failures/recovery, persistence, and shutdown in an isolated Compose project.
- [x] Record results and limitations; stop before M2. No provider access, financial schemas/data, scoring, deployment, commits, or pushes.

Initial environment: documentation-only checkout at `e9cbc86` on `main`; Node `24.19.0`, npm `11.17.0`, Docker Engine `29.7.2`, Compose `5.5.0`, Docker Desktop `4.89.0`; no host .NET SDK. Docker access requires tool elevation. Container verification will use a distinct task-owned project and new volumes. The existing stack-guidance file SHA-256 is `BD21BBCCE77D843826B92258047F700CAE1874F6FCA4980E3750120FED618316` and will remain unchanged.

#### M1 resolved versions and documentation

Resolved on 2026-09-05 from official release metadata, maintainer documentation, npm/NuGet metadata, and pulled official images. Exact direct package versions are declared in project files; transitive versions and integrity hashes are retained in lockfiles. No preview releases or forced peer-dependency overrides.

| Component | Exact choice | Official evidence and decision |
| --- | --- | --- |
| .NET | SDK `10.0.400`; ASP.NET/runtime/OpenAPI `10.0.11`; `net10.0` | [LTS policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [SDK policy](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json). Active LTS through 2028-11-14. Exact SDK, prereleases disabled, roll-forward disabled. |
| Node / npm | `24.20.0` / bundled `11.19.0` | [Node releases](https://nodejs.org/en/about/previous-releases), [exact release](https://nodejs.org/en/blog/release/v24.20.0), [npm ci](https://docs.npmjs.com/cli/v11/commands/npm-ci/), [package.json](https://docs.npmjs.com/cli/v11/configuring-npm/package-json/). Node 24 is active LTS; use pinned container for canonical checks instead of changing the host installation. |
| React | `19.2.8`; React DOM `19.2.8`; types `19.2.18` / `19.2.7` | [versions](https://react.dev/versions), [SPA guidance](https://react.dev/learn/build-a-react-app-from-scratch). Maintain the existing SPA architecture. |
| Vite | `8.2.2`; React plugin `6.1.1` | [Vite guide](https://vite.dev/guide/), [plugin](https://github.com/vitejs/vite-plugin-react/tree/main/packages/plugin-react), [TypeScript](https://vite.dev/guide/features#typescript), [proxy](https://vite.dev/config/server-options#server-proxy), [environment](https://vite.dev/guide/env-and-mode). Plugin requires Vite 8; Node 24 satisfies both engines. |
| TypeScript | `6.0.3`; Node types `24.13.3` | [TS 6.0](https://www.typescriptlang.org/docs/handbook/release-notes/typescript-6-0.html), [typescript-eslint support](https://typescript-eslint.io/users/dependency-versions/). Latest compatible stable 6.0 patch; latest TS 7.0.2 is outside the linter's `<6.1.0` support. Strict mode, explicit Node/browser type environments, alias paths without deprecated baseUrl. |
| TanStack Router | React router `1.170.32`; Vite plugin `1.168.35`; CLI `1.167.33` | [Vite integration](https://tanstack.com/router/latest/docs/installation/with-vite), [CLI](https://tanstack.com/router/latest/docs/installation/with-router-cli). File routes; router plugin before React; generate route tree before type checking. Published plugin peer range includes this router and Vite 8. |
| TanStack Query / Zod | `5.102.8` / `4.5.4` | [Query setup](https://tanstack.com/query/latest/docs/framework/react/quick-start), [defaults](https://tanstack.com/query/latest/docs/react/guides/important-defaults), [cancellation](https://tanstack.com/query/latest/docs/framework/react/guides/query-cancellation), [Zod requirements](https://zod.dev/). Query provider configured without product requests or polling; per-query financial freshness awaits M4. Zod validates public shell configuration only. |
| shadcn/ui / Tailwind | Manual new-york Card source snapshot (2026-09-05); Tailwind and Vite plugin `4.3.3`; clsx `2.1.1`; tailwind-merge `3.6.0` | [Vite setup](https://ui.shadcn.com/docs/installation/vite), [manual setup](https://ui.shadcn.com/docs/installation/manual), [Card registry](https://ui.shadcn.com/r/styles/new-york/card.json), [Tailwind Vite](https://tailwindcss.com/docs/installation/using-vite), [clsx](https://github.com/lukeed/clsx), [tailwind-merge](https://github.com/dcastil/tailwind-merge). Vendor the single needed primitive with source/license attribution and components.json. No CLI install or animation/component suite is needed for this static shell. |
| Frontend lint | ESLint `10.10.0`, @eslint/js `10.0.1`, typescript-eslint `8.69.0`, hooks `7.1.1`, refresh `0.5.6`, globals `17.12.0` | [flat config](https://eslint.org/docs/latest/use/configure/configuration-files), [compatibility](https://typescript-eslint.io/users/dependency-versions/), [hooks](https://react.dev/reference/eslint-plugin-react-hooks), [refresh](https://github.com/ArnaudBarre/eslint-plugin-react-refresh), [globals](https://github.com/sindresorhus/globals). Published peer ranges support ESLint 10 and selected TS/Node. |
| Backend data clients | Npgsql `10.0.3`; StackExchange.Redis `3.1.31` | [Npgsql](https://www.npgsql.org/doc/basic-usage.html), [Npgsql package](https://www.nuget.org/packages/Npgsql/10.0.3), [Redis client](https://seredis.dev/Basics.html), [configuration](https://seredis.dev/Configuration.html), [package](https://www.nuget.org/packages/StackExchange.Redis/3.1.31). Infrastructure-only clients for SELECT 1 and PING health probes; no schemas/migration framework introduced. |
| PostgreSQL | `18.6-bookworm` | [support](https://www.postgresql.org/support/versioning/), [official image](https://hub.docker.com/_/postgres). Named volume at `/var/lib/postgresql`; image PGDATA `/var/lib/postgresql/18/docker`; UTC server/session defaults. |
| Redis | `8.10.1-alpine` | [official image](https://hub.docker.com/_/redis), [security](https://redis.io/docs/latest/operate/oss_and_stack/management/security/), [licenses](https://redis.io/legal/licenses/). Official Redis Open Source distribution (AGPLv3/RSALv2/SSPLv1 choices); unmodified local development service, private network, no persistence. Production hosting/licensing decision remains outside M1. |
| nginx | `1.30.4-alpine` | [stable release](https://nginx.org/en/download.html), [official image](https://hub.docker.com/_/nginx), [proxy](https://nginx.org/en/docs/http/ngx_http_proxy_module.html). Non-root static production shell with same-origin API proxy and runtime DNS resolution. |
| Docker tooling | Engine `29.7.2`, Compose `5.5.0`, Desktop `4.89.0` | [startup/shutdown ordering](https://docs.docker.com/compose/how-tos/startup-order/), [build practices](https://docs.docker.com/build/building/best-practices/). Existing supported tooling; tested baseline recorded, no global upgrade. |
| Operational verification | Node `24.20.0` built-in assertions/test APIs and Docker CLI | [Node test APIs](https://nodejs.org/docs/latest-v24.x/api/test.html), [Compose CLI](https://docs.docker.com/reference/cli/docker/compose/). Behavior checks over real isolated services; no broad testing framework or generated transport client in M1. |

Official base-image digests (multi-platform manifests, resolved by pull):

| Image tag | SHA-256 digest |
| --- | --- |
| mcr.microsoft.com/dotnet/sdk:10.0.400-noble | `e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c` |
| mcr.microsoft.com/dotnet/aspnet:10.0.11-noble | `a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94` |
| node:24.20.0-bookworm-slim | `ba849c60be29959425b8734d57b8b4b7d56f98edd9504c9af091d5281095a71e` |
| postgres:18.6-bookworm | `1c59e2c3c818eaa0f0628f695b36e7c9e362d6b219b36a54a32df645cbd7e1af` |
| redis:8.10.1-alpine | `becdda6c7f4b3fb42e42fd7f120bbf5c54c4caaaf16f26da24e4563d2c1f0576` |
| nginx:1.30.4-alpine | `dc5069ad14f19660b141b21236140b91656bf89bbc3e2417c70ae650cd66104c` |

M1 operational decisions: API and worker are composition roots sharing Infrastructure → Application → Domain. The latter two are empty extension points, with no speculative financial types. Worker uses an ASP.NET host only for private operational HTTP checks and a cancellation-aware BackgroundService. [Worker guidance](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers), [health guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0), [JSON log formatting](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/console-log-formatter).

Liveness excludes data dependencies; worker liveness additionally checks recent loop progress using monotonic elapsed time. Readiness requires PostgreSQL; Redis unavailability is explicitly Degraded/HTTP 200 because no M1 operation requires Redis coordination. PostgreSQL unavailability is Unhealthy/HTTP 503 without killing either host. Worker lifecycle/run scopes and request scopes carry correlation IDs; logs use UTC. All probes have bounded waits and propagate cancellation where client APIs accept it; Redis PING has a client timeout and cancellable wait because its API has no CancellationToken parameter. Later coordination-dependent jobs must revisit their readiness policy.

Use built-in [OpenAPI 3.1](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0) in Development only at `/api/openapi/v1.json`. Expose operational endpoints only, no sample weather or ranking DTOs. Use [problem-details/exception/status middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) with sanitized errors and correlation. Full OpenAPI client generation and financial wire conventions remain M4. [NuGet locks](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) are enforced by locked restore after initial resolution.

#### M1 verification results (2026-09-05)

Implementation: [README](../../../README.md), [Compose](../../../compose.yaml), [production-target override](../../../compose.production.yaml), [backend solution](../../../backend/Analysis.slnx), [frontend package manifest](../../../frontend/package.json), [isolated integration verifier](../../../scripts/verify-m1.mjs). AGENTS and roadmap status text now reflects M1 only. Architecture/product requirements remain unchanged.

The main local check project was `analysis-m1-20260905`. Automated regression verification generated its own fresh project `analysis-m1-check-49c628b67191`, with new ports, in-memory credentials and a new PostgreSQL volume. Its sanitized report is retained locally in `.artifacts/analysis-m1-check-49c628b67191.json` (ignored, reproducible with the verifier), SHA-256 `E0804B5502115EB051BF87BFB7ED7E4789EB17889FB8F9635A1A8546F171E495`. The first integration report is also retained and identifies the corrected Vite shutdown assertion. No prior data service or volume was used for failure testing. Final production-target builds passed after the explicit nested environment-file exclusions were added to `.dockerignore`; their log is retained in `.artifacts/final-build.log`.

| Check | Result | Evidence / scope |
| --- | --- | --- |
| Backend restore and compilation | Passed | Official SDK 10.0.400 container; six projects restored in locked mode and built Release with zero warnings/errors. API/worker publish succeeds. Six NuGet lockfiles retained. |
| Backend operational regression checks | Passed | Package-free executable under `backend/tests/Analysis.OperationalChecks`; real HTTP 404/405/500 problem details, valid/invalid correlation, unsupported Accept fallback, request cancellation, and worker unstarted/active/stalled/recovered/stopped states. Runs during backend image build; test routes never enter application images. |
| Frontend dependencies | Passed | Node 24.20.0 / npm 11.19.0; strict engines and peer resolution, `npm ci` from retained lockfile; 215 installed packages, audit reported zero known vulnerabilities at check time. No forced/legacy peer overrides. |
| Frontend typecheck/lint/build | Passed | CLI route generation precedes TypeScript; ESLint zero errors/warnings; Vite production build succeeds. shadcn Card source SHA-256 `525C4BB2C051987BE64DF0E92E1D90174912B219BF541E24FFBC4A3406DE49E8` and MIT license retained. |
| Compose configuration/startup | Passed | Default and production override resolve. A fresh five-service stack reaches Healthy with health-based dependencies. Images are version/digest pinned and application images are multi-stage. |
| Health and UTC | Passed | API and worker live/ready endpoints report actual dependency states; worker heartbeat advances independently. Raw health JSON uses UTC offsets; PostgreSQL SHOW timezone is UTC and backend JSON log timestamps end in Z. |
| Redis outage/recovery | Passed | Both hosts stay live; readiness is HTTP 200 Degraded during loss, returns to Healthy automatically after Redis restart. |
| PostgreSQL outage/recovery | Passed | Both hosts stay live; readiness is HTTP 503 Unhealthy during loss, returns to Healthy after PostgreSQL restart; application container IDs unchanged and restart counts zero. |
| OpenAPI / proxy | Passed | Development emits OpenAPI 3.1.1 with `/api/health/live` and `/api/health/ready`; both OpenAPI and health are reachable via Vite's `/api` proxy. Production returns 404 for OpenAPI and serves health through nginx. No hand-written frontend transport model. |
| Browser | Passed | In-app browser loaded the development shell, navigated About, and reloaded the production `/about` deep link; expected headings/copy and layout rendered, with no browser warning/error entries. No financial data or score placeholders. |
| Non-root / network privacy | Passed | Actual UIDs: development frontend 1000, production nginx 101, API/worker 1654. PostgreSQL/Redis/worker have no published ports; data network is internal. Frontend/API ports bind to 127.0.0.1. |
| PostgreSQL persistence | Passed | Verified named mount `/var/lib/postgresql` and PGDATA `/var/lib/postgresql/18/docker`; a committed scratch sentinel survived forced PostgreSQL container recreation. Scratch table was removed in its isolated verification database. No application/financial schema created. |
| Graceful shutdown | Passed | API/worker/PostgreSQL/Redis exit 0, worker emits correlated graceful-stop lifecycle log. Development Vite exits 143 after its awaited server close; production nginx and all four other production services exit 0. No OOM kills or forced SIGKILL. |
| Cleanup / unrelated work | Passed | Both automatically generated check projects had their own containers/networks/scratch volumes removed. Main check containers/networks removed, its `analysis-m1-20260905_postgres-data` volume retained. No running containers remain. Existing stack-guidance document hash is unchanged. No commits/pushes/deployments/provider access. |

Resolved verification findings:

- Fast Refresh lint required separating route declarations from component modules; checks rerun successfully.
- Exception middleware clears response headers, so request correlation now uses `OnStarting` to preserve the header on HTTP 500. Regression checks demonstrated the failure and pass after repair.
- Health endpoints use typed minimal routes over the built-in HealthCheckService so OpenAPI describes their operational response shapes. See [OpenAPI endpoint metadata](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0).
- The first shutdown assertion incorrectly required zero from Vite. The selected [Vite 8.2.2 source](https://github.com/vitejs/vite/blob/v8.2.2/packages/vite/src/node/server/index.ts) awaits `server.close()` and preserves the [Node signal exit convention](https://nodejs.org/docs/latest-v24.x/api/process.html#signal-events). The verifier now records every exit status and permits 143 only for development frontend; the complete integration script then passed.

**Failed checks remaining:** none. **Unavailable required checks:** none. Host .NET was unavailable, so restore/build/tests used the exact official SDK container. A checksum-verified official Node 24.20.0 Windows archive under ignored `.artifacts/` ran the host-side Docker verification script; the system Node 24.19.0 installation was unchanged. Browser verification used the available in-app browser, without adding Playwright dependencies.

**Remaining limitations:** TanStack Router CLI emits a circular-import warning mentioning `replaceRouteChunk`; route generation, typecheck, lint, builds and actual navigation pass. This is a tooling warning, not a waived failing check. The source snapshot for the single shadcn Card is the component pin; no shadcn CLI runtime was installed. No CI pipeline, migrations, providers, financial precision/units transport, rankings client, authentication or production deployment is included in M1. Browser checks were manual automation of the shell; later product interaction tests belong to their milestones. Image/package pins require deliberate updates.

**Next milestone at the M1 handoff:** M2 — Catalog and adapter. M2 had not started at that checkpoint. Its subsequent offline implementation and remaining licensing/access gate are recorded below. M3–M5 and overall slice acceptance remain incomplete.

### M2 — Catalog and adapter

#### M2 authorization and checklist (2026-09-05)

The user authorized M2 only and then explicitly chose **continue offline and document the licensing blocker**. No provider credentials, paid services, live data ingestion, deployment, commits or pushes. Existing M1 files and the pre-existing research document must be preserved. Tests use documented examples and explicitly labelled synthetic contract variants in disposable databases; these are never product data.

- [x] Inspect current repository, source-of-truth documents, tools and M1 evidence.
- [x] Validate candidate capabilities and identify licensing/access blockers before dependent work.
- [x] Select one migration authority and resolve exact compatible package/tool versions.
- [x] Implement canonical BTC/ETH/SOL catalog, separate provider instrument identities and normalized observations.
- [x] Implement offline-testable market, derivatives and chain-fundamentals adapters with bounded/cancellable reads, pagination, validation and structured failures.
- [x] Implement atomic, idempotent PostgreSQL persistence with payload provenance and visible quarantine for invalid/conflicting inputs.
- [x] Verify mappings, time/precision/units, retries/cancellation, migrations, duplicate/concurrent writes and preservation of existing rows in isolated containers.
- [x] Rerun affected M1 checks; document reproducible commands, evidence and remaining blockers. Stop before M3.

#### M2 provider decisions

These are **technical candidates for offline adapter development**, not approved live/product providers. No SLA, present asset availability, commercial display/storage rights or regional access is inferred from an API being public. Before live use, validate current instrument metadata and authorized endpoint/region, and resolve storage, derived-data and redistribution rights. The worker remains operational-only by default; M2 transport is restricted to loopback fixture servers. No environment flag silently enables live providers.

| Candidate / scope | Official evidence checked | Decision and limits |
| --- | --- | --- |
| Binance Spot REST v3, BTCUSDT/ETHUSDT/SOLUSDT | [Market API](https://developers.binance.com/en/docs/catalog/core-trading-spot-trading/api/rest-api/market), [public-data-only endpoint](https://developers.binance.com/docs/binance-spot-api-docs/faqs/market_data_only), [official source](https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md), [terms](https://www.binance.com/en/terms) and [published agreement, clause 27](https://bin.bnbstatic.com/static/cms/cg08ou2ak0tn7mcplvfg/file/9958035d95bff024a86a662fb4fdba45527d55a936e2cb9ed1d7ddf205ca082d.pdf) | Market adapter only: `/api/v3/klines`, 1h UTC bars, limit 1000, open/close times in milliseconds, base volume and USDT quote volume. Preserve USDT as USDT. Public read endpoints need no key; publication/product-use rights are unresolved. Metadata validation uses `/api/v3/exchangeInfo`. No historical-depth/SLA guarantee assumed. |
| Bybit V5, linear USDT perpetuals for the three assets | [Funding history](https://bybit-exchange.github.io/docs/v5/market/history-fund-rate), [OI](https://bybit-exchange.github.io/docs/v5/market/open-interest), [instruments](https://bybit-exchange.github.io/docs/v5/market/instrument), [integration/region guidance](https://bybit-exchange.github.io/docs/v5/guide), [rate limits](https://bybit-exchange.github.io/docs/v5/rate-limit), [terms entry](https://www.bybit.com/en/help-center/article/Bybit-Limited-Terms-and-Conditions) | Derivatives candidate selected because OI unit and both-sides convention are explicitly documented. Funding settlement records are fractions; no hard-coded historical 8h assumption. OI uses `openInterest` (both sides) in base-asset units, 1h sample interval; do not silently substitute `singleOpenInterest` or infer USD notional. Limit 200; OI cursor paging; funding backward end-time paging. History bounded by instrument launch and available responses. Public API descriptions do not establish product data rights. EEA endpoint/product restrictions also remain unresolved; no regional workaround or live calls. |
| DeFiLlama Free API, Ethereum and Solana chain TVL | [API documentation](https://api-docs.defillama.com/), [official OpenAPI source](https://github.com/DefiLlama/api-docs/blob/main/defillama-openapi-free.json), [terms clauses 7–8](https://defillama.com/terms) | `/v2/historicalChainTvl/{chain}` provides timestamped USD chain DeFi TVL excluding liquid staking and double-counted TVL. Chain ecosystem metric, not token revenue, token intrinsic value or native-token collateral. BTC is inapplicable, never zero. No key/payment for the free endpoint; commercial use/republication needs permission. Historical response has no range/pagination parameters or promised cadence/SLA; retain provider observation timestamps, filter requested range locally, and do not invent a daily-close convention. Advertised schema URL returned HTML; source-controlled official schema was read instead. |

Alternatives evaluated: Binance USD-M OI documentation names quantity/value but leaves their units less explicit than Bybit's selected endpoint; not implemented. Coin Metrics Community describes non-commercial access, so it does not resolve product licensing. DefiLlama's open-source SDK/source licenses do not override hosted data terms. No full vendor DTO schema or live dataset is vendored. Fixtures cite official documentation and label every changed example value or identifier.

#### M2 version and persistence decisions

Retain all M1 SDK/runtime/image/frontend pins. New exact versions resolved from NuGet's official flat-container metadata on 2026-09-05: **Microsoft.EntityFrameworkCore / Relational / Design 10.0.11**, **Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3**, **dotnet-ef 10.0.11**. The provider's nuspec requires EF `[10.0.4,11.0.0)` and Npgsql `>=10.0.3`, compatible with the selected packages, .NET 10 and PostgreSQL 18. No preview packages. Pin the local tool manifest and retain updated NuGet locks.

**Single migration authority:** EF Core migrations in Infrastructure, generated with the pinned local tool. Explicit worker maintenance command applies migrations; no `EnsureCreated`, init SQL, automatic startup migration, or competing migration system. Down migration is destructive to M2 tables: only test rollback on a disposable database; real rollback uses a prior backup or forward repair. [EF 10 LTS](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew), [Npgsql 10](https://www.npgsql.org/efcore/release-notes/10.0.html), [EF CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet), [applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [properties/precision](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties), [transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [Npgsql UTC](https://www.npgsql.org/doc/types/datetime.html).

Use .NET `decimal` and PostgreSQL unconstrained `numeric`, with explicit application precision bounds rather than a database scale that rounds input. Provider numeric text is validated before conversion; timestamps must fit millisecond UTC precision. Keep provider payload bytes and SHA-256 plus request/window metadata in Infrastructure; normalized observations reference payload identity and adapter mapping version. Duplicate identical facts are no-ops; conflicting same-key facts are quarantined without overwriting the original. Preserve missing observations as gaps. No feature/scoring tables, model weights, derived metrics or financial API DTOs in M2.

HTTP uses .NET 10 built-in `HttpClient`/`System.Text.Json`, cancellation and explicit bounds; no vendor SDK or new HTTP dependency. [HttpClient guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines), [JSON DOM](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-dom). Tests extend the existing package-free executable-check approach, adding a separate M2 checks project; no frontend dependencies or OpenAPI changes.

The offline implementation stores catalog seed data through the initial generated migration `20260905210022_M2CatalogObservations`. [EF model-managed seed data](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) is appropriate for the fixed three-asset/eight-ref catalog; seed entries remain candidate identities until live metadata is checked. Transaction-scoped [PostgreSQL advisory locks](https://www.postgresql.org/docs/18/explicit-locking.html#ADVISORY-LOCKS) serialize writes per instrument; composite primary keys and payload/instrument foreign keys guard duplicates and cross-instrument lineage. Payload bytes and metadata are committed atomically with their facts. Conflicting/invalid inputs produce idempotent quarantine codes and window metadata; raw rejected bodies are not retained. No silent correction of existing facts.

Transport limits are implementation bounds, not claimed provider allowances: 30-day requested window, 32 pages per endpoint, 4 MiB per response, 15-second header/body waits and three HTTP attempts. `Retry-After` and Bybit reset headers are respected; waits above 10 seconds terminate the attempt rather than retry early. HTTP 403/401 and structural mapping errors are permanent for the run. Redirects and non-loopback destinations are rejected. Live provider pacing, scheduled jobs, retry scheduling beyond these attempt bounds and current instrument coverage remain for the blocked M2 live follow-up.

#### M2 verification and handoff (2026-09-05)

**Authorized offline scope: passed. Live/provider gate: blocked, by the user's explicit offline decision pending rights/access evidence.** No live market-data endpoint was called; official documentation/package registries were the only provider-related external reads. No existing/provider credentials were accessed, services purchased, deployment performed, or Git commit/push made. Disposable database passwords were generated solely for the isolated checks.

Implementation files: Domain `Catalog.cs` / `Observation.cs`; Application `ObservationIngestion.cs`; Infrastructure `Adapters/` and `Persistence/` (including generated migrations); worker maintenance/refusal commands; Infrastructure package references and affected lockfiles; solution, Dockerfile, local EF tool manifest; `backend/tests/Analysis.CatalogChecks/`; `scripts/verify-m2.mjs`; README; data-source/roadmap status and this plan. M1 frontend, Compose configuration, operational implementation, AGENTS, architecture and pre-existing research guidance were not edited by M2.

| Check | Result / evidence |
| --- | --- |
| Restoration, compilation and migration drift | Passed with the pinned SDK, exact EF tool/packages and locked restore. Seven projects compile with zero warnings/errors; API/worker images publish. Generated migration matches the current EF model. |
| Official example and synthetic contract mappings | Passed. Unmodified Binance non-1h and Bybit inverse/USDC examples are rejected for the selected context. Labelled fixture variants cover BTC/ETH/SOL spot/perpetual identities and ETH/SOL chain TVL. No fabricated market history enters a product host. |
| Precision, units and timestamps | Passed. Exact decimal roundtrip including 1e-18 and 28-digit values; excessive precision, wrong JSON numeric types, missing fields and non-UTC/sub-ms inputs fail closed. Spot quote remains USDT, OI remains base-asset/both-sides, funding remains fraction and TVL remains USD. |
| Windows / missing inputs | Passed. Incomplete/out-of-window candles are excluded, missing candles are not interpolated, BTC has no TVL ref. No historical funding interval or TVL daily-close convention is invented. |
| HTTP and paging | Passed over an actual loopback HTTP server: metadata identity/category checks, backward funding pagination, encoded OI cursors, repeated/missing cursor rejection, HTTP 429 and Bybit 10006 retries, long Retry-After termination, permanent 403 refusal, redirect refusal, body-size bound and in-flight request cancellation. |
| PostgreSQL migration/integrity | Passed on a fresh disposable database: initial migration, seed data, destructive Down/reapply only before test observations, repeat migration over populated data, exact numeric/UTC storage and byte/SHA-256/mapping-version lineage. Initial representative fixture run writes 14 observations over eight refs. |
| Replay/concurrency/conflicts | Passed. Identical rerun preserves all rows/payloads/ingestion times. Four concurrent writes insert one new logical fact. Conflicting values leave original observations intact and create quarantine metadata. One invalid asset does not stop unrelated assets. Database rejects missing/cross-instrument lineage and NULL candle fields. In-flight advisory-lock wait cancellation leaves stored state unchanged. |
| Worker / persistence / cleanup | Passed. Explicit `--migrate` preserves the populated test database; full stored snapshot survives PostgreSQL container recreation. `--ingest-once` refuses with exit 2. Default worker remains healthy and stops with exit 0. Worker UID 1654 verified; its image contains neither test fixtures nor the checks executable. |
| M1 regression | Passed again: five healthy services, API/worker health and dependency loss/recovery, UTC/correlation/problem details, development OpenAPI/Vite proxy, non-root/private networking, PostgreSQL persistence and graceful shutdown. Backend image builds also rerun M1 operational checks. Frontend/browser code is unchanged; a new visual browser run and a new frontend lint/production build were not needed for this backend change. |
| README migration generation | Passed with the exact SDK/tool, without a database connection. Idempotent SQL was generated to ignored `.artifacts/m2-migrations.sql` and reviewed for M2 schema/history scope. |

Final M2 report: `.artifacts/analysis-m2-check-c810b06cd61b.json`, SHA-256 `412E75A8593A60CDC669266621B67B8A7871CD63BC01FCE818959C3947E992C7`. Canonical database snapshot hash before/after container recreation: `863d09f18dc3f4d8353b1c301a0ffb6e9c1686a2e7751e35a2e0abd59797162b`. M1 regression report: `.artifacts/analysis-m1-check-5dee8fa5c99c.json`, SHA-256 `68571B144FEDA0D91E63D44398D898A382BB61E64D1AD7D7C4D621F7E0AD7A02`. Earlier M2 reports are retained as intermediate evidence; final schema/type/cursor guards are covered by the final M2 run.

All task verification projects (`analysis-m2-check-6e469128bcd9`, `analysis-m2-check-f6019ed2086a`, `analysis-m2-check-c810b06cd61b`, and the M1 regression project) removed their own containers/networks/scratch volumes. The existing `analysis-m1-20260905_postgres-data` volume remains; unrelated running containers were not changed. The pre-existing research document still hashes to `BD21BBCCE77D843826B92258047F700CAE1874F6FCA4980E3750120FED618316`.

Resolved during verification: the Web SDK already includes JSON content, so fixtures use `Content Update` instead of duplicate `Include`; database shape constraints explicitly reject NULLs; payload foreign keys bind instrument identity; provider scalar types and OI cursor presence are enforced. **Failed checks remaining: none. Environmental blockers: none for the authorized offline scope.** The existing Router CLI warning is unchanged. **Unavailable by scope/rights:** live provider access, actual current asset coverage and live drift/ingestion verification. No claim that the candidates are commercially licensed or that M2's live acceptance is complete.

- Canonical assets BTC, ETH, SOL
- ProviderInstrumentRef table/map
- One market-data adapter and one derivatives adapter (same vendor allowed if official docs cover both)
- One fundamentals source that provides a meaningful applicable series for ETH and SOL
- Normalized OHLCV, funding, OI (and other confirmed fields)
- Mapping tests from documented fixtures

### Frontend testing follow-up (2026-09-06)

User-authorized addition before further milestone work: add Vitest and Playwright to the existing empty shell. Backend already has package-free executable operational/catalog tests and disposable-container M1/M2 verifiers. Preserve that stack, the offline licensing gate, and all M1/M2 application behavior. No rankings contract, invented financial data, provider calls or M3 work.

Short implementation and verification checklist:

- [x] Inspect current tests and consult official documentation/registry metadata.
- [x] Pin compatible test dependencies and retain the npm lockfile.
- [x] Add isolated component/configuration tests and browser navigation, deep-link, fallback, keyboard and narrow-viewport checks.
- [x] Run clean restoration, strict type checking (including tests), lint, Vitest, production build and Playwright against the built SPA.
- [x] Document reproducible host/container commands, outcomes and remaining limits.

Resolved stable versions from the official npm registry on 2026-09-06 (no peer overrides):

| Dependency/tool | Exact version | Official documentation and compatibility decision |
| --- | --- | --- |
| Vitest | 5.0.0 | [Getting started](https://vitest.dev/guide/), [configuration](https://vitest.dev/config/), [environment](https://vitest.dev/guide/environment.html), [environment stubs](https://vitest.dev/api/vi#vi-stubenv). Stable registry release accepts Node 24 and Vite 8; use jsdom for components, imports rather than global test APIs. |
| Playwright Test | 1.63.0 | [Installation](https://playwright.dev/docs/intro), [webServer](https://playwright.dev/docs/test-webserver), [assertions](https://playwright.dev/docs/test-assertions), [fixtures](https://playwright.dev/docs/api/class-test#test-extend), [network interception](https://playwright.dev/docs/api/class-browsercontext#browser-context-route), [browsers](https://playwright.dev/docs/browsers), [Docker](https://playwright.dev/docs/docker). Requires Node >=20. Chromium, Firefox and WebKit use the matching package's browser revisions. Run a managed loopback preview of the production bundle with no server reuse, retries or live data. |
| React Testing Library / DOM Testing Library | 16.3.3 / 10.4.1 | [Setup](https://testing-library.com/docs/react-testing-library/setup/), [API/cleanup](https://testing-library.com/docs/react-testing-library/api/). React 19 and DOM 10 satisfy the declared peers. Real route tree with isolated [memory history](https://tanstack.com/router/latest/docs/framework/react/guide/history-types); no mocked router or transport DTOs. |
| jest-dom | 7.0.1 | [Maintainer Vitest setup](https://github.com/testing-library/jest-dom#with-vitest). Requires Node >=22, DOM >=10 <11; import the Vitest matcher entry in test setup. |
| jsdom | 30.0.1 | [Maintainer documentation](https://github.com/jsdom/jsdom). Node ^24.15.0 satisfies its runtime minimum with existing Node 24.20.0. Real layout/focus checks belong in Playwright. |
| Node / npm / Vite / React / TypeScript | 24.20.0 / 11.19.0 / 8.2.2 / 19.2.8 / 6.0.3 | Retain existing pins. [Node release policy](https://nodejs.org/en/about/previous-releases) confirms Node 24 LTS; compatibility is verified with strict restoration and executable checks below. |
| Official Playwright test image | v1.63.0-noble | `mcr.microsoft.com/playwright:v1.63.0-noble@sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27` (multi-platform index; amd64 manifest `sha256:bc6ab0d6d44ff4826e4cb8c1e6d801e185bfc42bb0753f8e2a30efc70db054c7`). Dedicated test target, non-root `pwuser`, existing pinned Node/npm copied from the dependency stage. Browser tests run with Docker network disabled and no host ports/volumes. Application production images remain unchanged except for running unit tests during their build. |

Each npm package's exact metadata was resolved from `https://registry.npmjs.org/<package>/latest` before installation; all new packages are development dependencies. Test reports, traces and browser caches are ignored. Coverage thresholds and hosted CI remain unresolved; adding runners does not implement M3–M5 tests.

#### Frontend testing results (2026-09-06)

**Complete for the authorized shell-testing follow-up.** No application UI, backend, Compose, provider gate or financial contract changes. Vite 8.2.2 is now an explicit exact development dependency instead of relying on the existing transitive resolution. Vitest configuration shares the real Vite plugins/alias; Playwright exercises their built output. Test TypeScript has its own strict project; production application code does not import test code.

| Check | Result |
| --- | --- |
| Clean locked installation | Passed: `npm ci` inside the fresh dependency stage, strict engines/peers, no overrides; npm reported zero vulnerabilities. Node 24.20.0/npm 11.19.0 assertions passed in both dependency and browser stages. |
| Vitest | Passed: 2 files, 9 tests (configuration defaults/trimming/length/rejection, empty workspace, analytics-only boundary, missing route). |
| Static checks and build | Passed: ESLint with zero allowed warnings, `tsc -b` including tests/configuration, Vite production build. The existing Router CLI `replaceRouteChunk` circular-import warning remains non-blocking. |
| Playwright | Passed: 16/16 tests, 4 scenarios across Chromium, Firefox, WebKit and narrow Chromium, zero retries. No runtime/console errors or attempted external/API requests. Container had no external network, published ports or data mounts. |
| Browser versions | Matching Playwright 1.63.0 manifest: Chromium/headless shell 153.0.8010.12 revision 1243; Firefox 155.0 revision 1543; WebKit 26.6 revision 2359; ffmpeg revision 1011. Verified on Linux/amd64 Ubuntu Noble. |
| Production image and runtime | Passed: production target build, UID 101, static index present, Node and application `node_modules` absent. Test runtime UID 1001. Test dependencies/browser binaries are excluded from the production image. |
| Cleanup/preservation | Passed: all created check containers removed, no new data volume/network, existing `analysis-m1-20260905_postgres-data` retained. Research document SHA-256 remains `BD21BBCCE77D843826B92258047F700CAE1874F6FCA4980E3750120FED618316`. `git diff --check` passed. Existing dirty M1/M2 work is preserved. |

Changed files for this follow-up: root `.gitignore`, `.dockerignore`, README; frontend package/lockfile, Vite/ESLint/TypeScript configuration, Dockerfile, new `tsconfig.test.json`, `playwright.config.ts`, `tests/unit/{setup.ts,config.test.ts,shell.test.tsx}`, `tests/e2e/shell.spec.ts`; testing strategy and this plan. No other subsystem files were edited.

Retained local verification evidence (ignored): `.artifacts/frontend-test-build-20260906.log` SHA-256 `4682E5C8AE62FB048817E7C6794A4C7948C8F456F3CB21BC7933392BE5F741AB`; `.artifacts/frontend-playwright-20260906.log` SHA-256 `2FA56C6E90EA422D0FD68B793B5AC8582E2F70C1F1CE386B7F6ABF94761FE9B3`; `.artifacts/frontend-production-build-20260906.log` SHA-256 `DE857BA7268D6F0B17C82D8A4929ED9678C319CAA82D160A98B5B1A10157E2CD`. Frontend lockfile SHA-256 `DA70423BE770CC4B906F5DD31737098A935ADBF6F36811F8F4B71CED15D77FA5`.

**Failed: none. Environmental blockers: none for Docker verification. Not rerun:** unchanged backend/M1/M2 service/database checks (their prior results remain above). Native Windows/macOS browser execution was not separately verified. No automated accessibility audit or physical-device certification is claimed; keyboard focus and viewport behavior were checked. CI and coverage thresholds remain future decisions. **Next milestone remains M3 — Features and scores**, after resolving the documented M2 live-source acceptance requirements; neither M3 nor the full vertical slice is complete.

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
- [x] UTC timestamps in API JSON (M1 operational responses verified; future financial endpoints require their own checks)
- [ ] API precision and unit conventions are documented; exact values use a round-trip-safe wire representation
- [x] Worker logs correlation of run id (M1 lifecycle; future job runs require their own checks)
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
- Canonical candle interval — M2 resolves spot candles to 1h UTC; OI samples to 1h. Funding/TVL keep provider event timestamps without an invented accrual/daily-close convention.
- .NET / Node LTS versions — resolved in M1 above
- Charting library (can remain unused in M5)

## Recommended next Codex task

The next work is the **M2 live-provider gate**: establish authorized storage/derived-data/display rights, applicable regional API access and current BTC/ETH/SOL instrument coverage (or choose documented alternatives). Then authorize and implement the live transport/worker run and verify a bounded real-data ingestion. The current task intentionally stops at the user-authorized offline scope. **M3 — Features and scores** is the next implementation milestone after M2's outstanding gate; it has not started. Do not begin M3–M5 or treat the entire vertical slice as complete.
