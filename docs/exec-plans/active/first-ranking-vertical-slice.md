# First ranking vertical slice

**Status:** Active execution plan. M1 and M2 are implemented and verified for the user's private, single-user use from Kosovo (2026-09-06). The bounded M2 live acceptance passed with all required data series. Commercial sharing/redistribution is deferred and requires a new rights review. **Next milestone: M3 — Features and scores.** M3–M5 have not started; the overall vertical slice is not complete.

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

#### M2 private-use completion — 2026-09-06

Resolved before implementation (official documentation reviewed 2026-09-06):

- Scope is the user's own local research from Kosovo, with no sharing, resale or
  commercial service. This is a limited-use implementation decision based on the
  terms below, not a grant of ownership or unrestricted redistribution rights.
  No account, API key or paid subscription is needed by the selected public endpoints.
- Binance: [terms](https://www.binance.com/en/terms), effective 2026-07-21,
  clause 27 permits necessary personal noncommercial/internal use of Binance IP.
  [Current prohibited countries](https://www.binance.com/en/about-legal/list-of-prohibited-countries),
  updated 2026-01-05, does not name Kosovo. Use only
  `https://data-api.binance.vision`, the documented
  [unauthenticated market-data host](https://developers.binance.com/en/docs/products/spot/faqs/market_data_only),
  for spot instrument metadata and closed hourly BTC/ETH/SOL USDT candles.
  [Klines](https://developers.binance.com/en/docs/catalog/core-trading-spot-trading/api/rest-api/market)
  accept UTC millisecond bounds and at most 1,000 rows per page.
- Bybit: [API terms](https://www.bybit.com/en/legal/service-specific-terms/API-Terms),
  updated 2026-01-16, sections 1.2/5.1 permit API development/use subject to limits;
  sections 6/7 forbid commercial exploitation, resale, identity masking and
  availability/performance benchmarking. [Platform terms](https://www.bybit.com/en/legal/terms-of-service/Bybit-BTL-Platform-Terms-and-Conditions),
  effective 2026-09-02, section 15 permits materials for the user's own use and
  prohibits further distribution without consent. [Restricted countries](https://www.bybit.com/en/help-center/article/Service-Restricted-Countries),
  updated 2026-09-01, does not name Kosovo. Use `https://api.bybit.com` only;
  regional rejection terminates that provider, with no alternate-host/proxy bypass.
  [Funding](https://bybit-exchange.github.io/docs/v5/market/history-fund-rate),
  [open interest](https://bybit-exchange.github.io/docs/v5/market/open-interest),
  [instruments](https://bybit-exchange.github.io/docs/v5/market/instrument) and
  [rate limits](https://bybit-exchange.github.io/docs/v5/rate-limit) remain authoritative:
  funding is a fraction; intervals come from metadata; linear OI is both sides
  in base-asset units, not the separate single-side field; pages max 200.
- DeFiLlama: [terms](https://defillama.com/terms), effective 2025-06-24,
  sections 1/7 cover official APIs and personal noncommercial use; section 8
  excludes resale/republication/commercial exploitation and unofficial API access.
  Use the [documented free API](https://api-docs.defillama.com/) at
  `https://api.llama.fi/v2/historicalChainTvl/{chain}` for Ethereum/Solana only.
  Limited private local observation/provenance storage is treated as incidental
  to this authorized personal research; no redistribution licence is inferred.
  No Kosovo-specific exclusion found. The free tier publishes no numerical SLA
  or retention guarantee. Fetch once per chain per run, then filter locally;
  BTC chain TVL remains inapplicable. Sharing/commercial use needs a new review.
- Retain SDK **10.0.400**, ASP.NET/.NET runtime and EF Core/Relational/Design/
  dotnet-ef **10.0.11**, Npgsql and Npgsql EF **10.0.3**, Redis client **3.1.31**,
  Node **24.20.0** / npm **11.19.0**, PostgreSQL **18.6**, Redis **8.10.1**,
  Docker **29.7.2** and Compose **5.5.0**. No dependency additions, peer overrides,
  lockfile changes or migration changes are planned. .NET 10 and Node 24 remain
  supported LTS: [.NET policy](https://dotnet.microsoft.com/en-us/platform/support/policy),
  [Node release policy](https://nodejs.org/en/about/previous-releases).
  Existing Dockerfile/Compose digest pins and EF migration ownership are retained.
  Compatibility must pass locked container restore/build and the existing checks.
  [Npgsql EF 10 guidance](https://www.npgsql.org/efcore/release-notes/10.0.html)
  confirms EF 10/PostgreSQL 18 support. Verifier subprocess handling follows
  [Node 24.20.0 maintainer docs](https://github.com/nodejs/node/blob/v24.20.0/doc/api/child_process.md)
  using argument arrays, no shell interpolation and hidden Windows child processes.
  The versioned nodejs.org docs fetch failed; the exact release-tagged maintainer
  source was used instead. No dependency/API was substituted.
- Implementation follows [.NET HttpClient guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
  (one pooled client per provider per run, finite lifetime),
  [worker cancellation](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers),
  [EF migration application](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
  and [Compose one-shot runs](https://docs.docker.com/reference/cli/docker/compose/run/).
  A separate Compose override adds worker egress only when explicitly selected.
  Live GETs use exact host/path allowlists, an identifying user-agent, no cookies,
  proxy, credentials or redirects, one request/second/provider, at most 128
  attempts/provider/run, existing three-attempt retries/10-second delay ceiling,
  15-second header/body budgets and 4 MiB payload ceiling. Stop further calls to
  a provider after access/rate/transport failure. The one-shot command requires
  explicit private-use/Kosovo scope and a closed UTC window of at most seven days;
  total run deadline is five minutes. Default worker startup does not ingest.
- Verification uses loopback fixtures for failure, retry and cancellation paths;
  never availability/load tests against providers. The authorized live check is
  a small closed three-day window followed by one identical-window ingestion to
  verify idempotence. Keep payloads and local database configuration in ignored
  private storage; publish only counts, safe errors, timestamps and hashes.

**Complete for this private-use scope.** The user confirmed Kosovo and authorized completing M2 for their
own private use. This supersedes the earlier offline-only authorization for
provider access whose applicable private-use terms have been reviewed. It does
not authorize commercial distribution, provider credentials, purchases, deployment,
trading, commits/pushes or M3. Existing M1/M2 observations, migrations, frontend
foundation and unrelated services/data must be preserved. Commercial storage/
display/redistribution approval is a later sharing/monetization gate, not a blanket
prerequisite for this private-use M2 verification. Historical evidence above stays
intact and describes the earlier offline scope.

Checklist:

- [x] Inspect the clean checkout, operating contract, source-of-truth documents,
  installed toolchain and existing offline adapters/worker/persistence.
- [x] Review current official private-use/API terms, Kosovo access, documented
  endpoints/units/history/rate limits; record selected sources and any unresolved
  access requirement before dependent live work. No regional bypasses.
- [x] Record exact retained versions and maintainer implementation guidance before
  edits. Preserve EF migrations as the sole schema authority and all lockfiles.
- [x] Implement a bounded, explicit one-shot live worker path with trusted official
  destinations, cancellation, pacing/retry budgets, safe summaries and provenance.
  Preserve loopback-only fixture tests and default operational worker behavior.
- [x] Verify locked restore/build, offline contracts and transport bounds, worker
  command handling, migrations/persistence and M1 regressions.
- [x] Verify bounded real BTC/ETH/SOL candles/funding/OI and ETH/SOL fundamentals
  where access permits, including identical-window replay, provenance/precision/
  UTC, failures and cancellation. Keep data private in isolated local storage.
- [x] Update README/provider/roadmap guidance and actual acceptance evidence;
  report any missing live verification accurately. Stop before M3.

Implementation and observed evidence:

- Added Infrastructure-local `IProviderHttp`, shared `JsonHttp` retry/body handling,
  `PrivateProviderHttp` policy and strict `PrivateIngestionRequest` parsing. The
  existing three mapping algorithms/versions remain unchanged. `OfflineHttp`
  still cannot use a live origin. Application ingestion now records the UTC
  ingestion timestamp after the provider read, rather than before it.
- Worker `PrivateIngestion` checks pending migrations and the exact seeded
  eight-reference catalog before creating live clients. The command is one-shot,
  cancellation-aware, bounded and correlated; normal startup remains operational
  only. `compose.m2-private.yaml` adds worker egress explicitly. No frontend,
  endpoint, transport DTO, schema, migration, lockfile, dependency or image pin changed.
  `Provider.ApprovalStatus=Unresolved` is retained for wider product approval;
  reviewed personal use is explicitly selected by the command, not a blanket
  provider approval seed or a silent global live flag.
- Private live report: `.artifacts/analysis-m2-private-2811dc2e2021.json`, SHA-256
  `EBE17A325407A52BC73A68F64C9904682688615B24A9C636F90F411E875D2936`.
  Window **2026-09-01T00:00:00Z ≤ event < 2026-09-04T00:00:00Z**.
  Each run made exactly **6 Binance + 9 Bybit + 2 DeFiLlama GETs**. No keys,
  authentication, proxies, redirected destinations, retries or payment were needed.
  Kosovo is user-declared; successful access was observed from this execution
  network, without an IP-geolocation claim or a future access guarantee.

  | Stored series | BTC | ETH | SOL | Observed units |
  | --- | ---: | ---: | ---: | --- |
  | Closed 1h spot candles | 72 | 72 | 72 | Base-asset volume; USDT prices/quote volume |
  | Funding settlements | 9 | 9 | 9 | Fraction |
  | 1h both-sides open interest | 72 | 72 | 72 | Base-asset units |
  | Chain TVL observations | Inapplicable | 3 | 3 | USD |

- **465 observations**, zero missing hourly buckets in candle/OI series and zero
  quarantines. The second identical-window run inserted **0** observations and
  recognized all **465** duplicates. Exact stored-observation snapshot before/after:
  `c8d4bf7bccd3fde430b040192067f04cdde31bf103f022bbdaaa7eb27fd23dfb`.
  Raw bytes replay to decimal-equal observations with UTC, units, identity and
  SHA-256 lineage checks. Response metadata changed, so payload count grew from
  **17 to 29**; original facts and their lineage did not change. This is expected
  provenance preservation, not duplicate observations.
- Reapplying migrations and recreating the private PostgreSQL container preserved
  the full final database snapshot:
  `0ebb4ec9293ab33e23637f7dd3d64f2ba3c85627d6f3def1d614e63a923bb86d`.
  Non-root worker UID **1654** verified. Retained only the new private data volume
  `analysis-m2-private-2811dc2e2021_postgres-data` and its ignored configuration
  `.artifacts/analysis-m2-private-2811dc2e2021.env`; all its containers/networks stopped
  and removed. Do not publish the volume, configuration or raw payloads.
- M1 regression: `.artifacts/analysis-m1-check-998cc6257b0f.json`, SHA-256
  `1A6F1152B4240702814469B4D28D02C532A389047ED8BF0FB227782A3A4D7ECD`.
  Passed five-service configuration/build/health, OpenAPI 3.1, Vite proxy/HTTP
  loading, problem details/correlation, non-root/private networks, Redis/PostgreSQL
  loss and recovery, PG 18 persistence and graceful shutdown. Its own scratch
  resources were removed. No provider outage/load benchmarking was performed.
- Offline M2 checks include strict scope/window parsing; credential-free fixed
  destinations; cross-instrument request/rate limits; long Retry-After refusal;
  redirect/access-denial stop; HTTP and pacing cancellation; EF drift/migrations,
  exact decimals, provenance, duplicate/conflict behavior and DB concurrency.
  Added a real one-shot SIGTERM check during blocked catalog I/O in disposable
  PostgreSQL, before any provider transport exists: exit **130**, correlated
  cancellation logs, no provider call. Final report:
  `.artifacts/analysis-m2-check-20547d6a6e79.json`, SHA-256
  `04FF0D7DEC63D0DA626B1CD67A035F45C64F7902A324CF4A2CB8C6E06077E57F`.
  **Failed checks remaining: none. Unavailable checks in private M2: none.**
  The final offline rerun covers the post-live logging-scope correction; no third
  live batch was needed. Both Node verifier scripts pass syntax checks and
  `git diff --check` passes. Docker **29.7.2** / Compose **5.5.0** rechecked.
- Intermediate failures preserved: initial build failed on an ambiguous nested
  target-typed constructor; specifying `ReadWindow` fixed it. The expanded verifier
  initially selected both a stopped one-shot and normal worker ID; removing its
  own stopped one-shot before normal-worker checks fixed the verifier. Neither
  failure changed retained user data or required a repeated live batch.
- Updated README, architecture, provider/testing guidance and roadmap. No providers
  contacted outside the two bounded data runs. Existing
  `analysis-m1-20260905_postgres-data` is preserved. The original stack research
  document still hashes to
  `BD21BBCCE77D843826B92258047F700CAE1874F6FCA4980E3750120FED618316`.

Changed file scope: `backend/src/Analysis.Application/ObservationIngestion.cs`;
Infrastructure's three adapter constructor types, `OfflineHttp`, new
`IProviderHttp`, `JsonHttp`, `PrivateProviderHttp`, `PrivateIngestionRequest` and
test friend-assembly declaration; worker `Program.cs` and new `PrivateIngestion`;
CatalogChecks `Program.cs`, `DatabaseChecks`, new private transport/database checks;
`scripts/verify-m2.mjs`, new `scripts/verify-m2-private.mjs` and
`compose.m2-private.yaml`; README, architecture, provider/testing guidance,
roadmap and this plan. No commit, push or deployment was performed.

Limits: this is private local ingestion, not scheduled scanning or a commercial
licence. A three-day sample does not prove arbitrary historical coverage, all
future funding intervals, uptime, or freshness SLAs. Provider revisions conflict
and quarantine rather than overwrite existing facts. No financial values are
displayed in the shell; feature definitions/weights and golden vectors remain M3,
generated rankings contract/client remains M4, dashboard data remains M5. Frontend
type/lint/Vitest/Playwright were not rerun because frontend files/pins were unchanged;
the M1 integration build/HTTP checks passed and prior browser evidence is retained.
**Stop before M3; do not mark the overall slice complete.**

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

### Frontend integration foundation (2026-09-06)

**Implemented and verified — user-approved foundation follow-up.** Preserves `new-york`, the existing palette/layout, M1/M2 and the offline licensing gate. No public table page, showcase, financial queries/DTOs, new backend endpoints, preset application or M3–M5 implementation. Table inspector is explicitly deferred: its required [unified devtools shell](https://tanstack.com/devtools/latest/docs/overview) is alpha; the user selected stable-only tools.

Implementation checklist:

- [x] Reinspect repository, approved skill and exact registry metadata; record official sources and compatibility.
- [x] Pin dependencies; run shadcn context/docs and preview component additions before adding them.
- [x] Share a stable QueryClient through typed Router context/providers with fresh test factories; integrate stable development-only inspectors.
- [x] Add typed Table v9 rendering/controlled sorting with shadcn primitives and React Icons; verify Zod 4 URL validation in test-only routes.
- [x] Enable official lint rules; run clean restoration, strict type checking, unit/browser tests and production exclusion checks.
- [x] Rebuild frontend Docker targets, verify non-root runtimes, preserve visual behavior and update guidance/results.

Exact versions confirmed from official npm metadata before installation (2026-09-06):

| Package | Pin | Official documentation / compatibility decision |
| --- | --- | --- |
| `@tanstack/react-table` | 9.2.4 | [React v9 quick start](https://tanstack.com/table/latest/docs/framework/react/quick-start), [sorting](https://tanstack.com/table/latest/docs/framework/react/guide/sorting). Requires Node >=20 / React >=18; use `useTable`, explicit sorting features, stable columns/data and typed `ColumnDef`, not v8/legacy APIs. |
| `react-icons` | 5.7.0 | [Maintainer README](https://github.com/react-icons/react-icons), [Lucide collection](https://react-icons.github.io/react-icons/icons/lu/). Named `react-icons/lu` imports; React peer `*`; retain MIT package and Lucide ISC notices. |
| Query / Router devtools | 5.102.8 / 1.167.1 | [Query inspector](https://tanstack.com/query/latest/docs/framework/react/devtools), [Router inspector](https://tanstack.com/router/latest/docs/devtools). Peers accept installed Query 5.102.8, Router 1.170.32 and router-core 1.171.27. Gate a lazy import with `import.meta.env.DEV`; do not enable production entrypoints. |
| Query / Router ESLint plugins | 5.102.8 / 1.162.0 | [Query flat configuration](https://tanstack.com/query/latest/docs/eslint/eslint-plugin-query), [Router flat configuration](https://tanstack.com/router/latest/docs/eslint/eslint-plugin-router). Both accept ESLint 10; Query accepts TypeScript 6. Use recommended flat configs without disabling correctness rules. |
| `shadcn` CLI | 4.21.0 | [CLI](https://ui.shadcn.com/docs/cli), [components configuration](https://ui.shadcn.com/docs/components-json), [Table](https://ui.shadcn.com/docs/components/radix/table). Requires Node >=20.18.1; install locally as a dev dependency and invoke via the pinned npm toolchain. No initialization, base switch, preset or global skill installation. |

Retain Query 5.102.8, Router 1.170.32, Zod 4.5.4, React 19.2.8, Vite 8.2.2, TypeScript 6.0.3, Node 24.20.0/npm 11.19.0 and existing test/image pins. Registry endpoint pattern: `https://registry.npmjs.org/<package>/<version>`. No peer overrides; lock all transitive resolutions. [Router context](https://tanstack.com/router/latest/docs/guide/router-context), [external Query loading](https://tanstack.com/router/latest/docs/guide/external-data-loading), [query options](https://tanstack.com/query/latest/docs/framework/react/guides/query-options), [cancellation](https://tanstack.com/query/latest/docs/framework/react/guides/query-cancellation), [testing](https://tanstack.com/query/latest/docs/framework/react/guides/testing), [Zod search validation](https://tanstack.com/router/latest/docs/guide/search-params), [Zod 4](https://zod.dev/) are integration references. Query retains existing no-polling defaults; financial freshness policy remains M4.

Official skill authority: [shadcn `SKILL.md`](https://github.com/shadcn-ui/ui/blob/c257f688cf4de7ec10cc1be84cad29cd4631182c/skills/shadcn/SKILL.md) at immutable revision `c257f688cf4de7ec10cc1be84cad29cd4631182c`, including relevant styling, composition, icon and component-base references. User requirements override floating `@latest` examples and sample icon imports: use shadcn 4.21.0 and `react-icons/lu`. Official registry selection is already authorized. Read actual component docs returned by the CLI, inspect additions with dry-run/diff and preserve existing custom components. Do not invent an `iconLibrary` value unsupported by the shadcn schema.

Component resolution before installation (2026-09-06): pinned CLI `info --json` confirmed Vite, Tailwind v4, Radix base, `new-york`, existing Card only and no preset. `docs table button` returned the [Radix Table](https://ui.shadcn.com/docs/components/radix/table) and [Radix Button](https://ui.shadcn.com/docs/components/radix/button) pages, which were read; `add @shadcn/table @shadcn/button --dry-run` and `--view` previewed two new files without CSS/config changes. Registry output imports `radix-ui` and CVA; resolve `radix-ui` **1.6.7** (React 19 peers supported; [introduction](https://www.radix-ui.com/primitives/docs/overview/introduction), [Slot](https://www.radix-ui.com/primitives/docs/utilities/slot)) and `class-variance-authority` **0.7.1** ([stable installation](https://cva.style/getting-started/installation/)). The registry also requests `cn` **0.2.5** ([maintainer](https://github.com/shadcn-ui/cn), Node >=20); allow it during generation, then adapt generated imports to existing `@/lib/utils` and remove the redundant direct dependency. Do not migrate the existing utility or Card. Versions resolved via official npm package metadata before installation; stable versions only, no peer overrides.

Table v9 uses `useTable`, explicit sorting features and `table.FlexRender`; the [React sorting](https://tanstack.com/table/latest/docs/framework/react/guide/sorting) and [state ownership](https://tanstack.com/table/latest/docs/framework/react/guide/table-state) guides plus installed 9.2.4 declarations/bundled maintainer guidance were consulted. Use the supported `state.sorting` + `onSortingChange` API for this simple externally controlled component. Keep data/column/identity function references stable at the caller; external atoms and performance optimizations require a demonstrated need. No v8 constructor, filtering or pagination.

Final resolved component/tool internals include `@radix-ui/react-slot` **1.3.3**, `@tanstack/table-core` **9.2.4**, Query inspector core **5.102.8**, Router inspector core **1.168.1**. Router's React Store **0.9.3** and Table's nested React Store **0.11.1** remain separately resolved according to their supported dependency ranges; no forced deduplication. `cn` **0.2.5** remains only as a transitive CLI dev dependency. Every direct dependency has an exact stable version matching `package-lock.json`; lockfile SHA-256 `8BF486785D594BB9E8CA6F48D119D3EC413EB0EF62DE77FCDE33369BCD085B8F`.

Project adaptations: Table/Button use existing `@/lib/utils`; Button exports only its component to preserve the existing Fast Refresh lint rule. Existing semantic palette values remain unchanged; additional aliases supply primitive foreground/hover/focus colors and a standard destructive token for the new Button variant. No existing page uses that variant. Existing Card and `components.json` are unchanged. The workspace arrow uses named `react-icons/lu`; React Icons MIT, Lucide ISC and applicable Feather MIT notices are retained in `frontend/public/react-icons-licenses.txt` and the production artifact. Vendored Table SHA-256 `75C2355A58861229029512B6F221DDEE4E5A0E3326BFA5D2D3D0BCCF35E25CD6`; Button `16403632A7E4C9A13B88E64CC77C2F24F0D6065FBF07027543172BA740E01BC9`. Existing `SHADCN-LICENSE.md` covers the added primitives.

Verification tooling references: [Vite plugin hooks](https://vite.dev/guide/api-plugin), [Rolldown emitted chunk/module information](https://rolldown.rs/reference/Interface.OutputChunk), [Playwright browser APIs](https://playwright.dev/docs/api/class-page), [visual comparisons](https://playwright.dev/docs/test-snapshots), and [Node 24.20.0 subprocess documentation](https://github.com/nodejs/node/blob/v24.20.0/doc/api/child_process.md). The Vite build hook audits emitted modules rather than relying only on hidden inspector controls. A disposable-container negative check deliberately imports devtools and must fail that guard.

Verification evidence (2026-09-06, pinned Linux Docker toolchain):

| Check | Result | Evidence |
| --- | --- | --- |
| Clean dependency installation | Passed | Docker dependencies stage ran `npm ci` as `node`, exact Node 24.20.0/npm 11.19.0, strict peer checks, 636 packages installed / 637 audited, zero vulnerabilities. No peer overrides. |
| Strict types, official recommended lint configs, Vitest, production build | Passed | 5 test files / **18 tests**; app/config/test TypeScript compiled; ESLint zero warnings/errors; Vite production build passed. `build-production-final.log` SHA-256 `214D7E1EF7C7F8157FFF04C97647C33A4F4E89766A15EC7BAA09898E55B260AA`. |
| Router/Query/Zod/Table coverage | Passed | Shared provider/context client survives StrictMode/rerenders; factory caches isolated; test-only direct Zod 4 URL validation defaults/rejects invalid input before loading; loader and hook share fresh cached data. Table semantics, custom cells, caption/empty state, canonical row identity, externally owned ascending/descending/clear sorting and reset verified with note fixtures. |
| Existing production browser projects | Passed | **16/16**, Chromium/Firefox/WebKit/narrow Chromium; existing shell scenarios retained. No runtime errors, API/external requests, inspector controls or devtool resource loads. `browser-r1.log` includes these passes and the historical development test failure below. |
| Development inspectors | Passed after locator correction | **1/1** Chromium development scenario; Query opens/closes with an empty actual cache, Router panel follows `/` → `/about` → browser back. No product requests/errors. `browser-dev-r2.log` SHA-256 `2B75F8B3664BA93599ACA7FBCD318A24B1DEA8C313EC05EBA48698D673C6F4FA`. Runs under `pwuser` UID **1001**, `--network none`; no backend required. |
| Production exclusion | Passed | 124 rendered modules audited; no devtools/test/CLI modules. Forced devtools import in a disposable container correctly failed the build (expected exit 1; negative-check wrapper passed). Final nginx contains static assets/notices, no `/app/src`, `/app/tests` or `/app/node_modules`. |
| Style and keyboard preservation | Passed | Existing keyboard skip-link/navigation scenarios pass in all four projects. Before/after Chromium captures at 1280×900 and 390×844 have identical palette/font and body/header/main/footer/h1/Card geometry records; matching JSON SHA-256 `DAE11C96246719A0B85C3B8BF86B4ABA7A8905EA57B036535B522896C2EC6691`. Screenshots inspected. Existing Card source hash remains `525C4BB2C051987BE64DF0E92E1D90174912B219BF541E24FFBC4A3406DE49E8`. |
| Docker targets and runtime | Passed | `e2e`, `development`, `production` rebuilt. Actual development UID **1000** and nginx UID **101**; both healthy; `/` and `/about` load, development inspector module served, production `/healthz` returns 200. Runtime containers use `network=none`, no host ports or volumes. |
| Shutdown and cleanup | Passed | nginx exits **0**, Vite **143** after SIGTERM (documented M1 behavior), no OOM/forced kill. Task containers removed, existing `analysis-m1-20260905_postgres-data` retained and unrelated services untouched. |

Local evidence, reports, failed-run traces and before/after screenshots are retained under ignored `.artifacts/frontend-foundation-20260906/`. Earlier failures are preserved: sort-action text composition required a separating text node; the Zod rejection test originally assumed an error-name string instead of checking validation issues; Router devtools overrides its toggle label with `Open TanStack Router Devtools`, so the browser locator was corrected and ineffective override props removed. All affected checks were rerun successfully. The final production static JS/CSS hashes match the browser-verified build; the subsequent addition is the icon license notice asset.

Changed scope: `frontend/` manifest/lockfile, application/cache factories, root context/provider, development inspectors, Table v9 adapter and shadcn Table/Button, workspace icon, semantic CSS tokens, license notices, lint/build guard, Vitest/Playwright tests/configuration and Docker test command; `AGENTS.md`, `ARCHITECTURE.md`, `README.md`, testing guidance and this plan. Backend, Compose, financial models/adapters and existing research guidance are unchanged (research guidance SHA-256 remains `BD21BBCCE77D843826B92258047F700CAE1874F6FCA4980E3750120FED618316`).

**Remaining failed checks: none. Unavailable Docker checks: none. Not rerun:** unchanged backend compilation/operational/catalog/M1/M2 verifiers and full five-service Compose checks; their historical evidence remains above. Native Windows/macOS browser execution, full accessibility audit, CI and coverage thresholds are not claimed. The existing Router CLI `replaceRouteChunk` circular-import warning remains non-blocking. Table has no product page, filtering, pagination or inspector; financial requests and generated transport/client validation remain M4. **Next milestone: M3 — Features and scores, after the outstanding M2 licensing/access/coverage acceptance gate.** M3–M5 and the overall vertical slice remain incomplete.

### Frontend Query conventions and feature structure — 2026-09-06

**Completed; authorized frontend-only follow-up.** Preserved the completed M1/M2
offline work and frontend foundation, all exact dependency/tool/image pins and the lockfile.
This decision supersedes the earlier application-wide Query overrides; historical
verification above describes the earlier implementation and remains preserved.

Implementation and verification checklist:

- [x] Move application assembly/config/layout/devtools into `src/app`, keep thin
  file routes, and move workspace/about components into `src/features/workspace`.
  Keep shared components/primitives and utilities outside features. Group unit
  tests by owner without changing the existing browser projects or route URLs.
- [x] Construct `new QueryClient()` without global defaults or default setters.
  Require reusable typed `queryOptions` factories (and `infiniteQueryOptions` for
  future infinite queries); derive cache keys from their returned options. Enable
  installed Query ESLint `flat/recommended-strict` without peer/package changes.
- [x] Prefer meaningful, pure, stable consumer-level `select` projections. Preserve
  complete cache data; validate in the query function/generated boundary, never in
  `select`. No identity selectors, fake product queries, polling or transport DTOs.
  Any justified per-query policy belongs in its feature factory with its reason;
  test-only policies remain local to test fixtures.
- [x] Verify default configuration, shared Router/Query context, cache isolation,
  Zod URL validation and selector/cache behavior; retain all Table/shell coverage.
- [x] Run strict type checking/lint, Vitest, production build, all production
  Playwright projects and development inspectors. Rebuild Docker test/development/
  production targets; verify non-root health/shutdown, production exclusion
  (including a negative guard check), visual/keyboard preservation and cleanup.
- [x] Update repository, architecture, testing and setup guidance; record actual
  verification results and limitations without starting M3.

Official evidence consulted before implementation:

- [Query options](https://tanstack.com/query/latest/docs/framework/react/guides/query-options):
  colocate key/function, reuse across hooks/loaders/cache calls, apply projections
  at the consumer; infinite queries have a separate typed options helper.
- [Important defaults](https://tanstack.com/query/latest/docs/framework/react/guides/important-defaults):
  retain Query's stale/refetch/retry/garbage-collection/structural-sharing behavior.
  Absence of queries already means the foundation performs no product requests.
- [Render optimizations/select](https://tanstack.com/query/latest/docs/framework/react/guides/render-optimizations):
  `select` observes successful cached data without replacing it; reuse module-level
  selectors or `useCallback` when captures require it. Do not throw/validate there.
- [Query ESLint configurations](https://tanstack.com/query/latest/docs/eslint/eslint-plugin-query)
  and [prefer-query-options](https://tanstack.com/query/latest/docs/eslint/prefer-query-options):
  installed **5.102.8** exports `flat/recommended-strict`, adding the options rule to
  the recommended correctness rules. Verified against installed maintainer source.
- [Router file routing](https://tanstack.com/router/latest/docs/routing/file-based-routing):
  retain the route plugin/generated tree and thin entries. Router's existing
  `defaultPreloadStaleTime: 0` delegates freshness to Query and is not a Query override.

No new libraries or tools are introduced. Retain Query **5.102.8**, Router
**1.170.32**, Zod **4.5.4**, Table **9.2.4**, React **19.2.8**, Vite **8.2.2**,
TypeScript **6.0.3**, Node **24.20.0 LTS**/npm **11.19.0**, shadcn **4.21.0**,
Vitest **5.0.0**, Playwright **1.63.0**, ESLint **10.10.0**, all other foundation
pins and the exact Docker base digests recorded above. Compatibility remains the
previously verified stable release set; this follow-up adds no peer overrides or
lockfile changes. Backend structure and all provider/M2 acceptance gates remain
unchanged. **M3 — Features and scores** remains next after the outstanding M2 gate.

Verification evidence (2026-09-06, pinned Linux Docker toolchain):

| Check | Result | Evidence |
| --- | --- | --- |
| Clean install | Passed | Uncached `dependencies` target ran `npm ci` as Node UID 1000 using Node 24.20.0/npm 11.19.0, strict peers and retained lockfile; 636 packages installed / 637 audited, zero vulnerabilities. `clean-install.log` SHA-256 `09E7778AE9B502A3F38BD495943DCFB6F34997859B14989601DBCE3D0FF3955D`. No npm upgrade or dependency change. |
| Strict lint, types, unit tests and production build | Passed | Query strict recommended + Router recommended configs, zero lint warnings/errors; `tsc -b` passes; **6 files / 20 Vitest tests** pass. `build-e2e-r3.log` SHA-256 `6718D564BC0BA4169F3B0AA704FED83EB093F13D204A418427E0428611E8D86F`. |
| Query policies and integration | Passed | Factory global defaults are empty; source audit finds no global default configuration/setters. Shared context/client, independent caches and all Zod/Table/shell checks retained. Consumer `select` retains complete cached notes; unchanged input/function avoids recomputation, unrelated cache changes retain selected object identity, relevant changes reach the consumer. A disposable inline-query fixture fails specifically on `prefer-query-options` as expected. |
| Browser projects and inspectors | Passed | **16/16** production scenarios in Chromium, Firefox, WebKit and narrow Chromium; **1/1** development inspector scenario. No API/external requests or runtime errors, keyboard/navigation preserved, actual Query/Router inspectors work and are absent from production. Network-isolated test container UID **1001**. `browser-r1.log` SHA-256 `2E80CAAFE5DD1E289EF69E226C7FFF8B8B18051052384DD1EC4BFBD78A5A8A9D`. |
| Visual preservation | Passed | Desktop 1280×900 and mobile 390×844 captures inspected. Palette/font/layout records match the earlier foundation exactly, SHA-256 `DAE11C96246719A0B85C3B8BF86B4ABA7A8905EA57B036535B522896C2EC6691`. Manifest, lockfile, shadcn config, CSS and all three UI primitive hashes match the pre-refactor snapshot. |
| Docker/runtime/production boundary | Passed | Test, development and production targets rebuilt; development UID **1000**, nginx UID **101**, both healthy. `/` and `/about` load; relocated development-tools module serves successfully; production `/healthz` succeeds. Build audits **124 rendered modules**, excludes tests/devtools/CLI and rejects a forced relocated devtools import. nginx has no source/tests/node_modules; all six static files match the browser-verified build. Runtime containers have no network, ports or mounts. |
| Shutdown and preservation | Passed | nginx exits **0**, Vite **143** on SIGTERM within the 10-second stop timeout, no OOM/forced kill. All task containers removed. Existing `analysis-m1-20260905_postgres-data` retained; backend, Compose and research guidance unchanged. |

Evidence and browser reports are retained under ignored
`.artifacts/frontend-query-structure-20260906/`. Preserve two resolved initial
failures: `build-e2e-r1.log` caught a test fixture capture missing from its query
key; the fixture now uses a shared options factory and proves cache isolation by
updating only the second cache. `build-e2e-r2.log` caught possibly undefined mock
call access; expected fixture labels are now explicit and checked against the
loader. No rule suppression/non-null assertion was added; all affected checks
passed in run 3. The existing Router CLI `replaceRouteChunk` circular-import
warning remains non-blocking.

Changed files in this follow-up: moved `src/lib/{application,query-client,config}`
and application/provider/layout/devtools components into `src/app`, moved
`src/pages/{workspace,about}` into `src/features/workspace/components`, updated
`src/main.tsx`/the three route entry imports, strict Query lint and Vite's relocated
devtools exclusion. Regrouped five unit-test files, updated their imports/options
and Zod projection assertions, and added `app/query-select.test.tsx`. Updated
`AGENTS.md`, `ARCHITECTURE.md`, `README.md`, testing strategy and this plan. No
dependency, lockfile, image pin, UI primitive, CSS, browser scenario, endpoint,
transport contract, generated client, backend or provider changes in this follow-up.

**Remaining failed checks: none. Unavailable checks: none within this frontend
scope. Not rerun:** unchanged backend compilation/M1/M2 and five-service Compose
verifiers; earlier evidence remains above. Native Windows/macOS browser runs,
full accessibility audit and CI/coverage gates remain outside this verification.
There are still no product queries; the `select` behavior is demonstrated with
test-only nonfinancial fixtures. The M2 live licensing/access/coverage gate is
still outstanding. **Next milestone: M3 — Features and scores, after that M2
acceptance gate.** M3–M5 and the overall vertical slice remain incomplete.

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

- Concrete providers and private-use scope — resolved for M2 above; wider commercial redistribution remains unresolved until sharing/monetization is authorized.
- Exact feature list after doc validation
- Manifest weights
- Canonical candle interval — M2 resolves spot candles to 1h UTC; OI samples to 1h. Funding/TVL keep provider event timestamps without an invented accrual/daily-close convention.
- .NET / Node LTS versions — resolved in M1 above
- Charting library (can remain unused in M5)

## Recommended next Codex task

The M2 private-use acceptance gate passed on 2026-09-06. The exact next milestone is **M3 — Features and scores**: first finalize the documented 15–25 feature definitions and immutable scoring manifest, then implement deterministic feature/scoring jobs, append-only persistence with exact input lineage and golden-vector tests. Keep the existing private-use provider boundaries and revalidate any additional history or series before depending on it. M3 requires its own bounded implementation task; it has not started. Commercial redistribution review belongs before sharing/monetization, not as a blocker to this user's private M3 work. Do not treat M3–M5 or the overall vertical slice as complete.
