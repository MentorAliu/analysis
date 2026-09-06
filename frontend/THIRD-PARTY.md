# Third-party source

`src/components/ui/card.tsx` is the unmodified shadcn/ui new-york Card source
retrieved on 2026-09-05 from https://ui.shadcn.com/r/styles/new-york/card.json.
SHA-256: `525c4bb2c051987be64df0e92e1d90174912b219bf541e24ffbc4a3406de49e8`.
Its MIT license is preserved in `SHADCN-LICENSE.md`.

Manual setup follows https://ui.shadcn.com/docs/installation/manual and the Vite
guide. Only the required Card primitive, class helpers and theme tokens are
included. The shadcn CLI is not a runtime dependency. The vendored source itself
is the reproducibility pin; future registry changes are explicit source updates.

`src/lib/api/generated/` is generated from the local backend OpenAPI document by
`@hey-api/openapi-ts` **0.99.0** with its bundled Fetch client, TypeScript, SDK and
Zod 4 plugins. The generator's MIT license is retained in `HEY-API-LICENSE.md`;
source: https://github.com/hey-api/hey-api/tree/@hey-api/openapi-ts@0.99.0.
Regenerate with `npm run api:generate`; never edit generated files by hand.
The scoped `js-yaml` 4.3.1 security override and its official maintainer evidence
are documented in [the M4 contract](../docs/engineering/rankings-api.md).
