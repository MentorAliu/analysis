# Crypto analysis platform

Analytics and research platform for inspectable crypto-market signals. This
repository does not contain trading, order-routing, wallet, or custody flows.

## Milestone M1

M1 provides only the runtime foundation:

- React 19 + TypeScript + Vite frontend with TanStack Router, TanStack Query,
  Zod, Tailwind CSS, and shadcn/ui
- .NET 10 modular-monolith solution with separate API and worker hosts
- OpenAPI, RFC 9457 problem details, UTC health responses, JSON logs, request
  correlation IDs, and worker run IDs
- Docker Compose services for the frontend, API, worker, PostgreSQL, and Redis

Provider adapters, ingestion, scoring, domain persistence, migrations,
rankings, authentication, alerts, and charts begin in later milestones.

## Prerequisites

- Docker Desktop configured for Linux containers
- Optional for running outside containers: Node.js 24 LTS and .NET SDK
  10.0.400

Direct and transitive dependencies are pinned in `package-lock.json`,
`packages.lock.json`, `global.json`, project files, and immutable container
digests. Dependabot proposes controlled updates.

## Start the complete stack

Create local configuration and replace the placeholder PostgreSQL password:

```powershell
Copy-Item .env.example .env
```

Then start all five services:

```powershell
docker compose up --build
```

Available endpoints:

- Frontend: <http://localhost:5173>
- API metadata: <http://localhost:8080/>
- API OpenAPI document: <http://localhost:8080/openapi/v1.json>
- API liveness: <http://localhost:8080/health/live>
- API readiness: <http://localhost:8080/health/ready>

PostgreSQL and Redis bind only to loopback by default. Application containers
run as non-root with reduced privileges. Redis is disposable and has
persistence disabled for M1; PostgreSQL uses the `postgres-data` volume.
API and worker readiness checks verify both data services; liveness checks only
the host process.

Stop containers without deleting PostgreSQL data:

```powershell
docker compose down
```

## Frontend development

The Vite server proxies `/api/*` to the API and removes the `/api` prefix. The
default target is `http://localhost:8080`; Compose sets it to
`http://api:8080`.

```powershell
Set-Location frontend
npm ci
npm run dev
```

Useful checks:

```powershell
npm run lint
npm test
npm run build
```

The frontend Dockerfile's default `production` stage builds static assets and
serves them from an unprivileged NGINX process. Compose intentionally targets
the non-root Vite `development` stage so the development proxy is exercised.
The production NGINX stage also strips `/api` and proxies to the `api` service
on the same Docker network.

## Backend development

The solution uses nullable reference checking, warnings as errors, current
recommended analyzers, and Microsoft Testing Platform with MSTest.

```powershell
Set-Location backend
dotnet restore Analysis.slnx --locked-mode
dotnet build Analysis.slnx --configuration Release --no-restore
dotnet test --solution Analysis.slnx --configuration Release --no-build
```

Locked restore fails when project references and checked-in dependency closure
drift.

The API accepts a valid `X-Correlation-ID` header or creates one, returns it in
the response, adds it to problem details, and includes it in structured log
scopes. The worker creates a structured `RunId` scope for each host run.

Configuration comes from standard ASP.NET Core configuration providers.
Committed `appsettings.json` files include loopback PostgreSQL and Redis
samples without passwords. Compose overrides those connection strings through
environment variables. `.env` is ignored.

## Complete verification

```powershell
./scripts/verify-compose.ps1
```

The script builds and health-checks all five Compose services, verifies the
Vite proxy, then builds the production frontend and verifies its healthcheck
and same-origin API proxy. Its PostgreSQL volume is disposable and removed at
the end.

GitHub Actions runs frontend checks, backend locked restore/build/tests, and
this Compose smoke check. All application Dockerfiles are multi-stage and
their final runtime processes run as non-root users.

## Local operations

Inspect service health and logs:

```powershell
docker compose ps
docker compose logs api worker
```

Reset all local PostgreSQL data:

```powershell
docker compose down --volumes
```

Redis has no password in local Compose and must remain loopback-only. Set
`AllowedHosts` and provide production secrets through environment or a secret
store when deploying outside local Docker.
