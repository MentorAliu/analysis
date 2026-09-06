# Rankings API — M4 contract

**Status:** Implemented M4, 2026-09-06. Verification evidence is recorded in the
active execution plan. Private single-user research only.

`GET /api/v1/rankings`, operation ID `GetRankings`, reads one immutable stored batch.
Optional `modelId` defaults to `slice1-v1`; exact lowercase ID, 1-64 characters,
`[a-z0-9][a-z0-9._-]{0,63}`. Optional `asOfUtc` is a real UTC hour in
`YYYY-MM-DDTHH:00:00Z` form, no later than request time. It requires an exact match.
Omitting it selects greatest committed as-of for that model, regardless of creation
time or readiness. Unknown, incorrectly cased, duplicate, empty or malformed query
parameters return 400 before database access. No asset/sort/range/pagination filters.
Model absent and batch absent return distinct 404 codes; no fallback or model alias.

## Response

All listed fields are required, with unavailable values explicitly null.

- Envelope: `selection` (latest/exact), nullable `requestedAsOfUtc`, `retrievedAtUtc`,
  `asOfAgeSeconds`, `scoreUnit` (score-points), `batch`, `items`.
- Batch: `id`, `asOfUtc`, `knowledgeCutoffUtc`, `createdAtUtc`, `recordKind`,
  `inputHash`, `universeAssetIds`, `model`.
- Model: `id`, `manifestHash`, `calculatorSourceHash`, `featureVersion`,
  `scorerVersion`, `numericVersion`, `status`, `weightDenominator`.
- Item: canonical `assetId`, `symbol`, `name`, nullable `rank`, `scoreSnapshotId`,
  `featureSnapshotId`, `scoreHash`, `featureHash`, `state`, nullable `compositeScore`,
  `bullishConfidenceScore`, `bearishConfidenceScore`, `quality`, `categories`.
- Quality: `dataQualityPercent`, `contextCoveragePercent`, `providerAgreement`,
  `corePriceReady`, `featureStateCounts` (available/missing/stale/invalid/conflicted/inapplicable).
- Category: `category`, `state`, nullable `score`, `dataQualityPercent`,
  `applicableWeightNumerator`, `availableWeightNumerator`.

All scores/confidences/quality/coverage are exact plain decimal **strings with six
fractional digits**, formatted from decimal without floating point or new rounding.
Composite/category range is -100..100; confidence and percent range is 0..100.
Zero uses `0.000000`. Confidence is in score points, not percent probability.
Ranks/counts/weight numerators are bounded integers; age is integer seconds.
M4's OpenAPI transformer describes these as JSON integers without string coercion.
Age is bounded by 315537897599 (safe in JavaScript), without `int64` format: the
pinned generator otherwise emits a coercing BigInt validator. This was verified
against the actual exported schema and generated runtime code. Global JSON
number handling is unchanged. Exact string patterns also reject final newlines.
UTC timestamps end in Z and preserve stored millisecond precision. M1 operational
JSON and M3 internal canonical JSON are unchanged. Raw observations, feature
measurements, payloads and full manifests/replay documents are not exposed.

Complete and partial scores sort together by exact composite descending, then
canonical ID ordinal ascending. Ranks are unique sequential integers starting at 1.
Not-ready items follow in canonical order, with null rank and headline scores.
Every successful batch includes BTC/ETH/SOL, even if all are not ready. Categories
always occur as price, derivatives, fundamentals, regime. Missing/inapplicable
category scores remain null; inapplicable category quality retains M3's stored zero
placeholder, which is not a coverage assessment. Valid measured zeros stay zero.

`research-reconstruction` and stored knowledge cutoff remain explicit: historical
as-of does not mean the score existed then. Age measures elapsed whole seconds at
retrieval. Feature-state counts describe the original T/K assessment, not current
provider freshness. No live flag, invented SLA or freshness threshold. Provider
agreement remains `unassessed-single-source` for M3. Heuristics are not probabilities.

## Read, error and access boundaries

Use an untracked projection in read-only Repeatable Read, selecting one batch ID.
Only catalog/model/batch/score/category/feature snapshots and feature states are read;
no observations/payloads, providers, calculators, request replay, cache or writes.
Schema/read integrity failures fail closed; the API never migrates or repairs data.
Stored registered compatible models are readable without invoking a calculator.

400 invalid-query, 403 private-use-disabled, 404 model-not-found/batch-not-found,
405 method not allowed, 503 schema-not-ready/database-unavailable, 500 integrity or
unexpected failure. RFC 9457 problems include type/title/status and correlation/trace
IDs, optional instance/code, and field-keyed errors for validation. Error details
never disclose SQL, secrets, raw data or stack traces. Cancellation propagates through
all I/O, releases transactions and is not a server failure. Responses use no-store.

`Rankings:PrivateUseEnabled` defaults false; only private local Compose enables it.
API/frontend publishing stays loopback, database networking internal, proxies same
origin. API hosts are localhost, 127.0.0.1, [::1], api. No CORS or authentication
system is added. The flag is an exposure guard, not user authentication or a licence.
No deployment/sharing is authorized. Development-only OpenAPI remains unchanged.

## Generation and ownership

Pin `@hey-api/openapi-ts` 0.99.0 (Node >=22.18.0, TypeScript >=5.5.3/6 supported),
retain Node 24.20.0/npm 11.19.0, TS 6.0.3, Zod 4.5.4 and Query 5.102.8.
The generator's new `@hey-api/json-schema-ref-parser` dependency receives a scoped
`js-yaml` **4.3.1** override. Its initially resolved 4.2.0 was affected by the
maintainer's [GHSA-5p4m-2wfm-xmqj](https://github.com/nodeca/js-yaml/security/advisories/GHSA-5p4m-2wfm-xmqj);
[4.3.1](https://github.com/nodeca/js-yaml/releases/tag/4.3.1) is the published fix.
The [npm override](https://docs.npmjs.com/cli/v11/configuring-npm/package-json#overrides)
is limited to this new dependency subtree. Existing lock entries and direct/image
pins are preserved. Locked installation, generation and runtime tests establish
compatibility here; the advisory alone does not. The resolved graph audits clean.
Use TypeScript, SDK, bundled Fetch and Zod 4 plugins. Generate request/success
validation, disable transformations, explicitly validate HTTP errors with the
generated problem schema. SDK success validators do not validate error bodies.
Reject unexpected status/content types and redirects; propagate AbortSignal.

Export Development OpenAPI from an isolated local API; retain generated schema and
client in source control. Generation uses the local file only. Check regenerates to
temporary output and compares files/content; no hand-maintained TS/Zod DTO copies.
The frontend build checks generated drift; `verify-m4.mjs` additionally compares
the running API's document. Key order is normalized; array order is significant.
Shared lib owns generated transport; rankings owns transport use and queryOptions.
Normalize query keys using generated request validation/defaults. Keep the full
response cached. No Query global or per-query policy changes, selectors, consumers,
routes, prefetch, polling or dashboard are added by M4.

## Official evidence and verification

Planning inspected installed source/pins and hash-verified the recorded M3 reports;
that is separate from new M4 tests. Official mechanisms, consulted 2026-09-06:
[ASP.NET OpenAPI 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0),
[transformers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0),
[errors](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0),
[binding/cancellation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0),
[PostgreSQL 18 isolation](https://www.postgresql.org/docs/18/transaction-iso.html),
[EF read tracking](https://learn.microsoft.com/en-us/ef/core/querying/tracking),
[Hey API release](https://github.com/hey-api/hey-api/releases/tag/@hey-api%2Fopenapi-ts@0.99.0),
[npm metadata](https://registry.npmjs.org/@hey-api%2Fopenapi-ts/0.99.0),
[Zod](https://heyapi.dev/docs/openapi/typescript/plugins/zod),
[Fetch](https://heyapi.dev/docs/openapi/typescript/clients/fetch),
[Query options](https://tanstack.com/query/latest/docs/framework/react/guides/query-options),
[defaults](https://tanstack.com/query/latest/docs/framework/react/guides/important-defaults),
[cancellation](https://tanstack.com/query/latest/docs/framework/react/guides/query-cancellation).

Acceptance covers selection/validation, model isolation, concurrent atomic reads,
ranking ties/readiness/applicability, decimal/UTC/identity, generated success/error
validation and drift, actual cancellation, read-only SQL/table access, private
exposure, Redis independence, unchanged M3 hashes and M1-M3/frontend regressions.
Use synthetic fixtures and disposable databases; never run private acquisition
verifiers or mount retained private volumes. Record actual results in the active plan.
