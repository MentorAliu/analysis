# Agent operating contract

Crypto intelligence and signal-analysis platform. Continuously scan a liquid crypto market, analyze qualified assets, rank opportunities, explain score changes, store historical signals, and later measure whether those signals predicted subsequent returns.

This is an analytics and research product. Do not implement exchange trading or custody.

Treat these documents as the source of truth. Implement application changes through an active execution plan; do not invent provider capabilities, scoring weights, benchmark results, or probability claims.

## Source of truth

Read the relevant document before changing a subsystem. Do not copy detailed architecture into this file.

| Concern | Document |
| --- | --- |
| Product scope, workflows, MVP, non-goals | [docs/product/product-spec.md](docs/product/product-spec.md) |
| Product priorities, sequencing, and validation gates | [docs/product/roadmap.md](docs/product/roadmap.md) |
| System shape, modules, API, persistence, Docker | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Conceptual entities and relationships | [docs/design/domain-model.md](docs/design/domain-model.md) |
| Ingestion, normalization, jobs, timestamps, retries | [docs/design/data-pipeline.md](docs/design/data-pipeline.md) |
| Signal families, scoring, versioning, confidence | [docs/design/scoring-model.md](docs/design/scoring-model.md) |
| External provider capabilities and abstraction | [docs/engineering/data-sources.md](docs/engineering/data-sources.md) |
| Test layers and financial-data risks | [docs/engineering/testing-strategy.md](docs/engineering/testing-strategy.md) |
| First implementation slice | [docs/exec-plans/active/first-ranking-vertical-slice.md](docs/exec-plans/active/first-ranking-vertical-slice.md) |

Status labels used across docs: **Requirement**, **Proposed**, **Unresolved**, **Future**.

## Proposed repository map

Not yet scaffolded. When implementation starts, keep this layout unless a later exec plan changes it:

- `frontend/` — React + TypeScript + Vite SPA
- `backend/` — ASP.NET Core modular monolith (API host + worker host)
- `docs/` — product, design, engineering, and execution plans
- Docker Compose at the repository root for local development

## Invariants

Agents must not violate these:

1. No trading, order routing, wallet custody, or withdrawal flows.
2. Provider DTOs are adapter-local. They must not become domain models.
3. Raw observations, derived features, category/composite scores, and natural-language interpretations remain separate concepts.
4. Numeric scores are calculated by deterministic, versioned code. LLMs may explain structured results or extract qualitative catalysts; they must not compute scores.
5. Every persisted score identifies its immutable scoring-model version and the exact input feature snapshot needed for replay.
6. Expensive analysis runs only on Stage B qualified candidates, not the entire market universe.
7. Heuristic composite/confidence scores are not probabilities. Probability language requires historical calibration.
8. Stay a modular monolith. Do not introduce microservices, Kafka, or Kubernetes without a demonstrated requirement recorded in an exec plan.
9. PostgreSQL is authoritative. Redis is disposable cache and coordination.
10. All timestamps are UTC.
11. Official maintainer documentation is authoritative for library APIs, setup, recommended patterns, compatibility, and version-specific behavior. Do not rely on remembered APIs or third-party tutorials.

## Essential rules

- Use supported stable or LTS releases. Pin resolved dependency and tool versions at implementation time.
- Frontend integration: TanStack Router, TanStack Query, Zod, shadcn/ui. Generate API types/client from the backend OpenAPI contract; do not hand-maintain a parallel transport model. Validate untrusted runtime data without duplicating the contract by hand.
- Backend integration: cancellation-aware async I/O, RFC 9457 problem details, structured logs with correlation IDs, health checks, and secret-free config samples.
- One backend migration system owns application schemas. Do not mix EF migrations, Supabase migrations, or ad-hoc SQL as competing authorities.
- Money, prices, sizes, and percentages keep explicit units and decimal precision. Never mix percent and fraction representations.
- Identify assets by canonical asset identity, not by a single exchange ticker string.
- Ingestion and scoring jobs must be idempotent and replayable. See [docs/design/data-pipeline.md](docs/design/data-pipeline.md).
- Docker local development uses Compose. Production images are multi-stage and non-root. See [ARCHITECTURE.md](ARCHITECTURE.md).

## Validation

Before merging a change, satisfy the relevant layers in [docs/engineering/testing-strategy.md](docs/engineering/testing-strategy.md). At minimum:

- Deterministic scoring and feature tests for any scoring change.
- Provider mapping/contract tests for any adapter change.
- No OpenAPI/client drift for API contract changes.
- UTC, precision, unit, symbol, duplicate, and freshness checks for financial data.

## Official documentation

Consult current docs at implementation time:

- [React](https://react.dev/)
- [Vite](https://vite.dev/guide/)
- [TanStack Router](https://tanstack.com/router/latest)
- [TanStack Query](https://tanstack.com/query/latest)
- [Zod](https://zod.dev/)
- [shadcn/ui](https://ui.shadcn.com/)
- [ASP.NET Core](https://learn.microsoft.com/aspnet/core/)
- [.NET](https://learn.microsoft.com/dotnet/)
- [ASP.NET Core OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
- [Handle errors in ASP.NET Core](https://learn.microsoft.com/aspnet/core/web-api/handle-errors)
- [PostgreSQL](https://www.postgresql.org/docs/current/)
- [Redis](https://redis.io/docs/latest/)
- [Docker](https://docs.docker.com/)
- [Docker Compose](https://docs.docker.com/compose/)
