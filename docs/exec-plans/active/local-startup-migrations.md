# Single-command local startup

Status: implemented and verified, 2026-09-07.

The user authorized a local startup command that applies pending migrations before
the app starts. Previously, `compose up` alone left a fresh database without the
rankings schema and the dashboard returned `schema-not-ready`.

## Scope and decisions

- Add `scripts/start-local.ps1`, fixed to project `analysis-local`, root `.env`
  and the explicit base `compose.yaml`. Preserve existing environment files.
- Initialize missing configuration through the existing pinned Node initializer;
  build the app images with Docker's cache, with an explicit `-SkipBuild` option
  for unchanged images. No host Node or .NET installation is required.
- Stop existing local frontend/API/ordinary worker before migration; wait for
  PostgreSQL/Redis; invoke the existing EF-owned worker `--migrate`; start app
  services only on success. Check every native command exit code.
- The base Compose configuration and normal API/worker entrypoints remain
  unchanged. This supersedes the original M2 explicit-startup restriction only
  for this user-invoked local wrapper, not for retained research or forward runs.
- No provider acquisition, issuance, scoring, scheduling, schema/model changes,
  automatic rollback, deletion, commits or pushes. The prepared forward project,
  restore project and existing research volumes remain outside this command.

## Validation

- PowerShell parse check and simulated migration failure: no app startup after a
  failed migration; every invocation uses the fixed local project and base file.
- Run startup against the user's local app and repeat using existing images:
  migration succeeds/no-ops, services become healthy, rankings no longer report
  schema-not-ready. Missing scoring data is still an expected empty-resource state.
- Check unchanged base Compose/backend files and preserve newer work. Do not rerun
  full financial/provider suites for this orchestration-only change.

Official command references reviewed:
[Compose run](https://docs.docker.com/reference/cli/docker/compose/run/),
[Compose up](https://docs.docker.com/reference/cli/docker/compose/up/).

## Recorded results

- PowerShell syntax, fixed-project argument scope and default build/migrate/start
  ordering passed. A simulated migration exit 17 propagated and prevented app
  startup. The first simulation harness had a PowerShell scope error; correcting
  its call recorder required no application change.
- Two real `start-local.ps1 -SkipBuild` runs against `analysis-local` succeeded.
  The first applied all three migrations to its previously unmigrated database;
  the second reported no pending migrations. All five services became healthy.
- API readiness returned 200. The frontend's same-origin rankings proxy returned
  404 `model-not-found`, replacing 503 `schema-not-ready`, as expected without
  scored data. The HTTP check explicitly decodes PowerShell's byte-array response
  for `application/problem+json`; its initial text assumption was corrected.
- Evidence: ignored `.artifacts/local-startup-check.json` and
  `.artifacts/local-startup-repeat.log`. Existing runtime emitted a nonfatal
  missing Kerberos-library diagnostic; migration completed with exit 0. Initial
  EF history lookup also logged an absent-table error before creating the schema.
- Actual image rebuilding was unnecessary for this script-only change; real runs
  used existing local images. No full financial/provider suites were rerun.
  Base Compose, backend, prepared forward resources and retained research data
  were unchanged. No live provider operations were performed.
