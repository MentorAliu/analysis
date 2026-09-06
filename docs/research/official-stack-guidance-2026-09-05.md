# Official stack guidance for implementation

**Researched:** 2026-09-05. **Status:** Proposed implementation guidance, based on official maintainer documentation. No application dependencies were installed or tested in this review. Exact package versions and image digests remain to be resolved and pinned during implementation. This document does not expand the active execution plan.

## Recommended next task and mode

Use **Default mode to implement M1 only** of the [first-ranking vertical slice](../exec-plans/active/first-ranking-vertical-slice.md). The product boundary, architecture and milestone order already exist. Begin with a short implementation checklist and version-resolution step, then build and verify the skeleton in the same task. M2 provider selection and ingestion are separate work.

Plan mode is useful when choosing a new architecture or resolving ambiguous product scope. OpenAI recommends planning before difficult or ambiguous tasks and also recognizes repository execution plans. Using Default here is a repository-specific judgment: the next task is already bounded by an active plan. [Official Codex best practices](https://learn.chatgpt.com/guides/best-practices).

## Supported versions and reproducibility

| Component | Research finding | Implementation guidance |
| --- | --- | --- |
| .NET / ASP.NET Core | .NET 10 is the active LTS line; .NET 8 and 9 are in maintenance. | Prefer .NET 10. Resolve an actual SDK version separately from runtime/package versions. |
| Node.js | Node 24 is LTS; Node 26 is Current. Production guidance favors supported LTS lines. | Prefer Node 24 LTS and pin its exact patch and package-manager version. |
| React / Vite | React documents a Vite TypeScript SPA path, with explicit responsibility for routing and data fetching. | Retain the repo's SPA architecture. Resolve mutually compatible stable React, Vite and React-plugin releases. |
| TypeScript / Zod | Zod 4 is stable, requires strict TypeScript and documents testing against TypeScript 5.5 and later. | Resolve a supported stable TypeScript release and Zod 4 release; enable strict checking. |
| PostgreSQL | PostgreSQL 18 is supported; the version policy recommends the current minor release. | Prefer PostgreSQL 18, pin its current supported minor and official image digest. |
| Redis | Redis is intended for trusted clients behind the application. | Resolve the stable edition/version, applicable license and .NET client compatibility; keep it private and disposable. |

Sources: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [Node releases](https://nodejs.org/en/about/previous-releases), [React SPA guidance](https://react.dev/learn/build-a-react-app-from-scratch), [Vite setup](https://vite.dev/guide/), [Zod requirements](https://zod.dev/), [PostgreSQL versioning](https://www.postgresql.org/support/versioning/), [Redis security model](https://redis.io/docs/latest/operate/oss_and_stack/management/security/).

Vite's minimum Node engine requirement is not a recommendation to use an end-of-life Node release. Likewise, a documentation page's `@latest` example is not a reproducible project pin. Record the resolved versions, compatibility evidence and verification date in the execution plan. Commit dependency lockfiles when the implementation is eventually committed.

Use `global.json` with a real SDK version and an explicit prerelease/roll-forward policy. SDK selection is independent of the target runtime; do not put an ASP.NET runtime patch number into the SDK field. For exact reproducibility, prefer `allowPrerelease: false` and an exact SDK policy, kept consistent with build images. [Microsoft SDK selection documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

## Frontend decisions

Retain React, strict TypeScript, Vite, TanStack Router, TanStack Query, Zod and shadcn/ui. React generally encourages frameworks, but documents this SPA approach and specifically identifies TanStack Router and Query for routing and REST data fetching. The existing ASP.NET backend and dashboard workflow provide a concrete reason to retain the agreed architecture. This is an architectural judgment, not a claim that SPA is universally preferable. [React's documented tradeoffs](https://react.dev/learn/build-a-react-app-from-scratch).

- **Routing:** prefer the documented TanStack Router Vite integration for file-based routes. Its router plugin must precede the React plugin. Route generation must run before checks that consume the generated route tree. [Official Vite integration](https://tanstack.com/router/latest/docs/installation/with-vite).
- **Server state:** use TanStack Query when server data arrives. Explicitly choose freshness and polling policy instead of treating its default cached-data staleness as market-data freshness. Pass cancellation signals through the HTTP client. The API's observation timestamps remain the evidence of market-data age. [Query defaults](https://tanstack.com/query/latest/docs/react/guides/important-defaults), [query cancellation](https://tanstack.com/query/latest/docs/framework/react/guides/query-cancellation).
- **Validation:** use Zod for untrusted configuration and URL/search state. Generate transport validators from OpenAPI when supported, instead of duplicating backend DTOs by hand. [Zod](https://zod.dev/).
- **UI setup:** follow the current shadcn Vite installation, including Tailwind's Vite plugin and matching import aliases. Adopt only primitives needed by the skeleton. Do not mechanically copy older Tailwind setup or incompatible TypeScript configuration. [shadcn Vite guide](https://ui.shadcn.com/docs/installation/vite).
- **Checks:** run TypeScript checking separately from Vite bundling. A successful Vite build alone does not establish type safety. [Vite TypeScript behavior](https://vite.dev/guide/features#typescript).
- **Configuration:** browser-exposed `VITE_` values are public build inputs. Database passwords and provider keys belong on the backend. Use the existing `/api` development-proxy design. [Vite environment variables](https://vite.dev/guide/env-and-mode).

M1 needs a minimal application shell and integration setup, not a simulated ranking dashboard. Product queries, filters and generated rankings clients remain later milestones.

## Backend and API contract

Prefer the built-in ASP.NET Core OpenAPI support rather than adding overlapping document generators by habit. `AddOpenApi` registers generation and `MapOpenApi` exposes the document; the official example limits runtime exposure to development. .NET 10 defaults to OpenAPI 3.1, so downstream code generation must be checked against the actual emitted dialect. [OpenAPI overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0), [document generation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0).

Build-time OpenAPI generation can invoke application startup. Keep generation independent of provider access, migrations and incidental infrastructure side effects. Verify this when generation enters the build pipeline. This is especially relevant before adding provider configuration requirements. [Microsoft's startup caveat](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0).

Use the built-in problem-details service and exception/status-code middleware, with correlation information and sanitized errors. Preserve the repo's RFC 9457 requirement. Configure JSON handling deliberately; registering the service alone is not a substitute for testing actual error responses. [ASP.NET error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).

Keep API and worker as separate hosts in one modular monolith. Follow the worker-host and `BackgroundService` patterns, propagate cancellation, and log UTC timestamps even where a generic documentation example uses local time. [Worker Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers).

Define liveness separately from readiness. In this repo, dependency checks should describe whether the process can perform its current work; an API dependency check does not prove the worker loop is alive. Choose and verify a worker-specific health mechanism in M1. Redis failure policy should respect its disposable role. [ASP.NET health-check guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0).

**Proposed for M4:** evaluate Hey API as a generator candidate because official plugins document Zod v4 validators and TanStack Query v5 options. This is not an adoption decision. Test local OpenAPI input, exact selected versions, decimal strings, UTC timestamps, nullability, errors and cancellation; verify its license. Generated validators must actually be enabled in the SDK path to protect responses. [Zod plugin](https://heyapi.dev/docs/openapi/typescript/plugins/zod), [TanStack Query plugin](https://heyapi.dev/docs/openapi/typescript/plugins/tanstack-query).

## Persistence and containers

PostgreSQL `numeric` supports exact decimal storage; a declared scale can round an incoming value. Select precision and rounding policies explicitly when financial schemas are implemented. Npgsql maps UTC `DateTime` to `timestamptz` and permits `DateTimeOffset` there only with zero offset. `timestamptz` does not preserve an original time-zone identifier, and PostgreSQL timestamp precision differs from .NET. Test the intended round trip rather than relying on local machine settings. [PostgreSQL numeric types](https://www.postgresql.org/docs/current/datatype-numeric.html), [Npgsql timestamps](https://www.npgsql.org/doc/types/datetime.html).

**Proposed for schema work:** EF Core 10 with the compatible Npgsql provider is a candidate for the single migration authority. The provider has an official 10.0 release. Resolve this before the first application migration; M1 does not require speculative financial tables or migration execution at application startup. [Npgsql EF provider release](https://www.npgsql.org/efcore/release-notes/10.0.html).

Compose's dependency order alone does not establish readiness. Use health checks and `service_healthy` for required startup dependencies, then handle later disconnects in the application. [Compose startup behavior](https://docs.docker.com/compose/how-tos/startup-order/).

**PostgreSQL 18 detail:** the official image uses `/var/lib/postgresql/18/docker` for `PGDATA` and `/var/lib/postgresql` for the volume. Use the documented volume layout for the selected image instead of copying a pre-18 example. Verify persistence by recreating containers without deleting the test volume. [Official PostgreSQL image](https://hub.docker.com/_/postgres).

Use multi-stage production images, non-root application runtimes, small build contexts and `.dockerignore`. Pin resolved base-image digests and document the update process: a pinned digest does not automatically receive security patches. Keep PostgreSQL and Redis on private Compose networking; expose only necessary local application ports. [Docker build guidance](https://docs.docker.com/build/building/best-practices/), [Redis networking guidance](https://redis.io/docs/latest/operate/oss_and_stack/management/security/).

## M1 verification and limits

The implementation should prove clean dependency restore, backend compilation, frontend type-check/lint/build, valid Compose configuration, startup of all five services, API/worker health behavior, development OpenAPI access, the frontend shell in a browser and graceful shutdown. Exercise an unavailable dependency and recovery using only isolated task-owned services. Confirm non-root application runtime users and test-volume persistence.

Use assertions about observable behavior, not tests that mirror generated scaffolding. Report passed, failed and unavailable checks separately. If Docker or another runtime is unavailable, complete unaffected work and describe the missing proof; do not mark M1 fully verified. No provider calls, scoring experiments, payments, deployment, commits or pushes were performed in this research task.

Official testing references for the implementation: [ASP.NET integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), [Vitest setup](https://vitest.dev/guide/), [Playwright practices](https://playwright.dev/docs/best-practices). Recheck the relevant APIs and version compatibility when introducing these tools; do not install a broad testing stack without a concrete M1 check that needs it.
