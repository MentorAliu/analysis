# Crypto research workspace

M1 of the [active execution plan](docs/exec-plans/active/first-ranking-vertical-slice.md)
provides a React shell, ASP.NET Core API and worker, PostgreSQL, and Redis in Docker
Compose. M2 adds an offline catalog, normalized observations, EF migrations and
fixture-tested provider adapters. **Live ingestion remains disabled** until data
licensing, regional access and actual instrument coverage are resolved.
This is an analytics and research product. No features, scores, rankings, trading
or custody flows are implemented. M3 has not started.

## Prerequisites and version pins

Use Docker Desktop with Linux containers and Docker Compose. Verified with Docker
Engine **29.7.2**, Compose **5.5.0**, Desktop **4.89.0** on Windows. The default
setup builds with containers; a host .NET SDK is not required.

- .NET SDK **10.0.400** (`global.json`, exact match), ASP.NET/runtime **10.0.11**, .NET 10 LTS.
- Node **24.20.0** (`.node-version`) and bundled npm **11.19.0**, Node 24 LTS.
- React **19.2.8**, TypeScript **6.0.3**, Vite **8.2.2**.
- PostgreSQL **18.6**, Redis **8.10.1**, nginx **1.30.4** for the production frontend.

All base/service images use version tags plus immutable digests. npm and NuGet
lockfiles preserve the dependency graphs. The active plan contains the complete
version matrix, compatibility decisions, official documentation, and verification
results. TypeScript 6.0 is deliberate: the selected linter does not support TS 7.

## Start locally

From the repository root, in PowerShell:

```powershell
$nodeImage = 'node:24.20.0-bookworm-slim@sha256:ba849c60be29959425b8734d57b8b4b7d56f98edd9504c9af091d5281095a71e'
docker run --rm --mount "type=bind,source=$PWD,target=/workspace" --workdir /workspace $nodeImage node scripts/init-local.mjs
docker compose -p analysis-local config --quiet
docker compose -p analysis-local up --build --detach --wait --wait-timeout 120
docker compose -p analysis-local ps
```

The initializer creates an ignored `.env` with a random local database password;
it never overwrites an existing file. `.env.example` contains no password. Set
`FRONTEND_PORT` and `API_PORT` in `.env` if the defaults conflict. Keep the same
Compose project name to reuse its PostgreSQL volume. Do not change the generated
password on an existing database without explicitly managing its database role.

- [Frontend](http://127.0.0.1:5173): an empty research workspace with Workspace/About routes.
- [API liveness](http://127.0.0.1:5080/api/health/live) and [readiness](http://127.0.0.1:5080/api/health/ready).
- [Development OpenAPI JSON](http://127.0.0.1:5080/api/openapi/v1.json), emitted as OpenAPI 3.1.1.
- The same API paths work through the frontend's `/api` proxy.

Only frontend/API ports are published, bound to loopback. PostgreSQL, Redis and
worker operational HTTP remain on private Compose networking. PostgreSQL 18 stores
data under `/var/lib/postgresql/18/docker`, with the named volume mounted at
`/var/lib/postgresql`. Redis has persistence disabled and is disposable.

## Build and verify

The API/worker Docker build performs locked restore, Release compilation with
warnings as errors, operational and offline adapter regression checks, and EF
migration/model drift checking. The frontend production
build performs `npm ci`, lint, route generation, strict TypeScript checks, and Vite
bundling:

```powershell
docker compose -p analysis-local build api worker
docker build --file frontend/Dockerfile --target build .
docker compose -p analysis-local exec -T api dotnet Analysis.Api.dll --healthcheck /api/health/ready
docker compose -p analysis-local exec -T worker dotnet Analysis.Worker.dll --healthcheck /health/live
docker compose -p analysis-local exec -T worker dotnet Analysis.Worker.dll --healthcheck /health/ready
```

With the exact host SDK and Node/npm installed, equivalent source checks are:

```powershell
dotnet restore backend/Analysis.slnx --locked-mode
dotnet build backend/Analysis.slnx --configuration Release --no-restore
dotnet run --project backend/tests/Analysis.OperationalChecks --configuration Release --no-build --no-restore
dotnet run --project backend/tests/Analysis.CatalogChecks --configuration Release --no-build --no-restore
npm --prefix frontend ci
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend test
npm --prefix frontend run build
```

The .NET regression executable uses only the selected framework and application
references. Its exception/cancellation routes exist only in that test process.
It checks problem details, correlation including HTTP 500, unsupported Accept
fallback, aborted HTTP requests, and unstarted/active/stalled/recovered/stopped
worker health. It creates no database schema and needs no running data services.

Run the full isolated integration verification from the root with Node 24.20.0
and access to Docker Desktop:

```powershell
node scripts/verify-m1.mjs
```

This script generates a unique `analysis-m1-check-*` project, random local ports,
and an in-memory password. It first proves that no containers or volumes exist
for that project. It builds and starts all five services; checks health, UTC,
OpenAPI, the proxy, correlation, runtime UIDs, network privacy, and the PostgreSQL
volume layout; stops/restarts each data service; recreates PostgreSQL with a
committed test sentinel; and checks graceful shutdown and sanitized structured
logs. The scratch table belongs only to this newly created verification database.
Finally it removes only its own containers, networks and scratch volume, and
writes a secret-free pass/fail report to ignored `.artifacts/`. It does not modify
`analysis-local`, other projects, or their data. Automated browser verification is
separate and described below.

### Frontend unit and browser tests

Vitest 5.0.0 runs configuration validation and component tests using React Testing
Library and jsdom. Tests use fresh application/QueryClient/memory-history instances.
They cover shared Router/Query context, isolated caches, direct Zod 4 URL validation,
loader-cache reuse and Table v9 rendering, empty states, stable row identity and
externally controlled sorting with nonfinancial fixtures.
Playwright 1.63.0 tests the built SPA in Chromium, Firefox, WebKit and a narrow
Chromium viewport: empty state, navigation/history, direct links/reload, missing
pages and keyboard skip-link access. Browser tests fail on console/runtime errors
or attempted external/API requests. A separate development Chromium scenario opens
the Query/Router inspectors and verifies route changes; production checks ensure
the inspectors are absent. These tests need no backend, data or credentials.

With the pinned Node/npm versions, from the repository root:

```powershell
npm --prefix frontend ci
npm --prefix frontend test
npm --prefix frontend run test:watch
```

Stop watch mode with Ctrl+C before continuing. Install the browsers matching the
locked Playwright package, then run the suite (which rebuilds the SPA first):

```powershell
npm --prefix frontend run test:e2e:install
npm --prefix frontend run test:e2e
npm --prefix frontend run test:e2e:report
```

On supported Linux hosts, append `-- --with-deps` to the install command if system
browser libraries are missing. Browser installation downloads test tools; the
tests themselves use only `127.0.0.1:4173` (production preview) and `127.0.0.1:4174`
(development Vite). Both ports must be free: Playwright never reuses or stops an
existing server. It starts and shuts down its own servers. To run only the
development inspector check: `npm --prefix frontend run test:e2e:dev`.
HTML reports are in `frontend/playwright-report/`; failed-test traces/screenshots
are in `frontend/test-results/`. Both directories are ignored by Git and Docker.
The narrow project checks responsive layout, not physical mobile-device behavior.

The Docker alternative needs no host Node, browsers, database, ports or volumes:

```powershell
docker build --file frontend/Dockerfile --target e2e --tag analysis-frontend-tests:20260906 .
docker run --rm --init --network none --shm-size=1g analysis-frontend-tests:20260906
```

The build runs clean `npm ci`, lint, Vitest, strict type checking of application
and test code, and the production build. The test target combines the digest-pinned
official Playwright browsers with the same pinned Node/npm toolchain and runs as
`pwuser`. Browser tests run with external networking disabled; `--rm` removes only
that container. Reports inside this disposable container disappear on exit; use
the host commands above when retaining interactive traces. Production images also
require Vitest to pass during their build and contain only static frontend output.
The Vite production module guard fails if devtools, tests or CLI code is emitted.

Backend tests remain the two executable check projects shown above (they use
`dotnet run`, not test-framework discovery through `dotnet test`). Operational
checks exercise HTTP errors/correlation and worker cancellation/health; catalog
checks cover offline adapters, units, precision and timestamps. `verify-m1.mjs`
and `verify-m2.mjs` additionally cover service behavior and disposable-database
integration. Hosted CI and numeric coverage thresholds are not configured yet.

### Frontend integration and component maintenance

`createApplication` supplies the same stable QueryClient to the provider and typed
Router context. Query owns freshness with its global defaults unchanged; the shell
makes no data requests or polling. Always define reads with reusable typed
`queryOptions` factories (`infiniteQueryOptions` for infinite queries), reuse them
in hooks/loaders, and derive cache keys from their options. Prefer pure, stable
consumer `select` projections for subsets/view models while retaining full cached
data. Document any necessary per-query policy beside its feature factory and in
the active plan; keep test-only policies isolated. Query's strict recommended
ESLint configuration enforces options usage.

Frontend ownership is feature-driven: `src/app` owns assembly/providers/config/
layout/devtools; thin `src/routes` entries compose `src/features/workspace`
components. Keep feature queries/hooks/schemas/selectors with their feature as they
are introduced. `src/components` (including shadcn `ui`) and `src/lib` are shared;
they never import features. Features must not import app assembly/routes or another
feature's internals. Unit tests mirror ownership under `tests/unit/app`,
`tests/unit/features/workspace` and `tests/unit/components`. See the
[architecture](ARCHITECTURE.md#frontend-integration) and [agent rules](AGENTS.md#query-and-feature-ownership).

The reusable `DataTable` takes stable typed columns/data, a canonical `getRowId`,
caption, optional empty state and controlled `sorting`/`onSortingChange` props.
There is no public table page or financial demo. Query/Router devtools load only
in development; Table's inspector remains deferred because its unified shell is
alpha. API client generation and transport validation remain M4.

Use the [pinned official shadcn skill and project conventions](AGENTS.md#frontend-component-workflow).
With the pinned toolchain, inspect context/docs before component changes:

```powershell
npm --prefix frontend run ui -- info --json
npm --prefix frontend run ui -- docs table button
npm --prefix frontend run ui -- add @shadcn/table --diff
```

For new components, preview with `--dry-run` and `--view`, then add only reviewed
items. The local CLI is pinned to 4.21.0; do not use floating `@latest`, reinitialize
or apply a preset. Preserve new-york/Radix and current styling; use `@/lib/utils`
and named `react-icons/lu` imports. Component source and license are retained in
the repository, with provenance and exact versions in the active plan.

## Development

Default Compose runs Vite in Development, copying frontend source at build time.
After source edits, run `docker compose -p analysis-local up --build --detach --wait`.
For host-side frontend hot reload, keep the Compose API running and use the exact
Node/npm versions:

```powershell
npm --prefix frontend ci
npm --prefix frontend run dev -- --host 127.0.0.1 --port 5174
```

The host Vite proxy defaults to `http://127.0.0.1:5080`; Compose sets
`API_PROXY_TARGET=http://api:8080`. If needed, copy `frontend/.env.example` to
`frontend/.env.local` and set the target. `VITE_` values are public browser build
inputs, never secrets. Zod validates the public application name. TanStack Query
is configured without product requests, polling, or a hand-written transport DTO
layer; generation from the backend contract belongs to M4.

Backend dependency direction is API/Worker → Infrastructure → Application →
Domain. Domain contains canonical identities, observations and numeric/time rules;
Application owns ingestion ports and batch orchestration. Provider parsing, opaque
payload storage, EF mappings and operational clients stay in Infrastructure.

## M2 offline catalog and adapters

Technical candidates are Binance spot candles (1h, USDT quote), Bybit linear
perpetual funding and both-sides open interest (base-asset units), and DeFiLlama
chain TVL (USD) for Ethereum/Solana. BTC fundamentals are inapplicable. These are
not approved live providers: see the plan's official capability/terms evidence.
The adapters accept only loopback fixture HTTP servers and never follow redirects.
The ordinary worker remains a cancellation-aware operational heartbeat. Running
`worker --ingest-once` exits 2 with the unresolved access/licensing reason.

The schema stores eight candidate instrument refs, payload bytes/SHA-256/mapping
versions and observation lineage. Numeric inputs use exact decimal parsing with
at most 28 digits and 18 fractional places; PostgreSQL `numeric` adds no implicit
scale rounding. Timestamps are UTC with millisecond precision. Funding is a
fraction, OI is both sides in the base asset, and USDT is never labelled USD.
Gaps stay missing. Identical reruns are no-ops; conflicting records are quarantined
without changing original observations. Quarantine contains safe error codes and
window/identity metadata, not raw provider error messages. No scoring schema exists.

Run all M2 checks using Node 24.20.0 and Docker Desktop:

```powershell
node scripts/verify-m2.mjs
```

This builds the explicit `m2checks` image target, creates a fresh
`analysis-m2-check-*` project and a new `analysis_m2_checks` database, runs loopback
HTTP mapping tests and PostgreSQL migration/integrity tests, verifies persistence
across container recreation, and removes only its own containers/network/volume.
It reads `.env.example` and generates its own password in memory. Existing local
data and `.env` are not used. Reports are written to ignored `.artifacts/`.
The test fixtures and verification executable are excluded from API/worker images.
Fixtures are documentation examples or explicitly synthetic variants, never
observed financial history; they are not inserted by normal application startup.

**Migration owner:** EF Core/Relational/Design **10.0.11**, Npgsql EF provider
**10.0.3**, local `dotnet-ef` **10.0.11** in `backend/.config/dotnet-tools.json`.
The exact .NET 10 SDK and M1 image pins remain unchanged. To create the M2 schema
in your chosen local stack, with its PostgreSQL service already running:

```powershell
docker compose -p analysis-local run --rm --no-deps worker --migrate
```

This explicit maintenance command applies pending EF migrations and exits. It
does not fetch provider data or start the worker loop. Ordinary startup does not
apply migrations. Existing observations survive repeated migration application.
The initial Down migration drops M2 tables and data: rollback tests use only a
fresh disposable database. For retained data, use backup/restore or a reviewed
forward repair; do not run a destructive Down migration against local history.

For migration development with the exact host SDK, run from `backend/`:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Analysis.Infrastructure --configuration Release
New-Item -ItemType Directory -Force ../.artifacts | Out-Null
dotnet ef migrations script --idempotent --project src/Analysis.Infrastructure --configuration Release --output ../.artifacts/m2-migrations.sql
```

The design-time factory needs no database connection or credentials for generation.
Generated migrations under Infrastructure are the sole schema authority; never
add a competing init-SQL or `EnsureCreated` path. Review generated SQL before use.

## Health and logs

| Condition | API/worker liveness | API/worker readiness |
| --- | --- | --- |
| Dependencies available, loop advancing | 200 Healthy | 200 Healthy |
| PostgreSQL unavailable | 200 Healthy | 503 Unhealthy |
| Redis unavailable | 200 Healthy | 200 Degraded |
| Worker loop unstarted, stopped, or no progress for 10 seconds | Worker 503 Unhealthy | Worker 503 Unhealthy |

The default worker only advances a lifecycle heartbeat every two seconds. These times
describe process health, not market ingestion cadence or financial freshness.
PostgreSQL probes execute bounded `SELECT 1` requests. Redis PING has a one-second
client timeout and a cancellable wait; its client reconnects without restarting
the host. Liveness never requires data access. Revisit readiness when a future job
requires Redis coordination. Compose waits for healthy dependencies at initial
startup; the application handles subsequent disconnects.

```powershell
docker compose -p analysis-local logs --tail 100 api worker
```

Backend logs are JSON with UTC timestamps, request trace/correlation scopes, and
worker lifecycle run IDs. `X-Correlation-ID` accepts 1–64 ASCII letters, digits,
periods, underscores or hyphens; invalid values are replaced. Errors expose
sanitized problem details and correlation, without database/provider exceptions.

## Production image check and shutdown

This override verifies production artifacts locally; it is not a deployment or
production security configuration:

```powershell
docker compose -p analysis-local -f compose.yaml -f compose.production.yaml up --build --detach --wait --wait-timeout 120
docker compose -p analysis-local exec -T frontend id -u
docker compose -p analysis-local exec -T api id -u
docker compose -p analysis-local exec -T worker id -u
```

The frontend becomes a non-root nginx process with same-origin `/api` routing and
SPA deep-link fallback. API/worker use non-root multi-stage images. OpenAPI is
disabled in Production. Re-run the default `compose.yaml` setup to return to Vite
and Development OpenAPI.

```powershell
docker compose -p analysis-local stop
docker compose -p analysis-local down
```

Both preserve the PostgreSQL named volume; `down` additionally removes the
project's containers/network. Do not add `--volumes` when retaining local data.
Worker cancellation has a 10-second host deadline within the 15-second container
grace period; PostgreSQL has 30 seconds. No host SDK/runtime installation or
unrelated service is changed by these commands.

Vite 8.2.2 closes its server before returning the conventional SIGTERM exit code
143. The integration verifier accepts that code only for the development
frontend; backend/data services and the production nginx runtime exit zero.

## Updates and current limits

Resolve upgrades from official maintainer documentation and registries, update
exact package/tool pins and image digests together, regenerate lockfiles, and
rerun the affected checks. Digest pins require deliberate security updates.
Do not use force/legacy peer overrides to hide incompatibilities.

The Router CLI currently emits a dependency circular-import warning mentioning
`replaceRouteChunk`. Route generation, type checking, lint, builds and navigation
pass; the warning is recorded in the plan. No runtime adoption beyond the M1
shell is implied. M2 precision/unit/mapping checks are offline; live provider drift,
data rights and access verification remain blocked. Rankings contracts, feature
formulas, scoring and frontend data integration belong to M3–M5.
