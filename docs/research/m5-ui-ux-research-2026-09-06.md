# M5 UI/UX research brief

**Date:** 2026-09-06. **Status:** Research and recommendations only. This is not an
execution plan, an approved redesign, or authorization to implement M5.

The user wants an attractive, accessible, performant frontend with production
quality interaction design. The immediate product is a private BTC/ETH/SOL
rankings workspace. This brief connects current primary guidance to the actual
repository, rather than treating a generic dashboard template as the product.

Evidence labels used below:

- **Repository fact:** verified by reading current source/configuration or the
  local pinned CLI. Existing test results remain historical evidence, not tests
  rerun for this research.
- **Documented guidance:** standards, maintainer documentation or the originating
  design team's published guidance. A design-system convention is not a WCAG rule.
- **Recommendation:** an application-specific judgment to evaluate during planning.
- **Unverified:** visual appeal, usability, accessibility conformance and actual
  M5 performance have not yet been measured. No M5 UI exists to certify.

## Repository facts that constrain the design

Read with `AGENTS.md`, `ARCHITECTURE.md`, the product specification, roadmap,
testing strategy, active first-ranking execution plan and `docs/engineering/rankings-api.md`.

The installed frontend is React 19.2.8, TypeScript 6.0.3, Vite 8.2.2, Tailwind
4.3.3, Router 1.170.32, Query 5.102.8, Table 9.2.4, Zod 4.5.4, Radix 1.6.7 and
shadcn CLI 4.21.0. Hey API 0.99.0 generates the transport. Vitest 5.0.0 and
Playwright 1.63.0 are already installed. Verify pins again before implementation;
this research changes none of them.

The pinned `ui -- info --json` command confirms Vite, Tailwind v4, new-york,
Radix, the official registry, and installed Button, Card and Table primitives.
Repository instructions explicitly select named `react-icons/lu` imports even
though the CLI reports a different default icon-library value. Current public
shadcn examples also include newer base/style defaults; they are not permission
to migrate this project.

`src/index.css` uses a warm near-white background, white cards, dark text and a
deep teal primary color. It declares Inter with system fallbacks; declaring a
family does not establish that a font file is loaded. `app-layout.tsx` supplies a
centered `max-w-5xl` shell, top navigation, main landmark and skip link. Existing
layout/palette conventions must be respected; any broader shell or theme change
must be identified explicitly during planning.

`DataTable` already uses semantic table markup, a caption, scoped column headers,
sort buttons and `aria-sort`. It uses Table v9, stable input references and
externally controlled sorting. Its current interface has no row-expansion or
responsive column-visibility API. Any necessary shared extension must remain
generic; shared components cannot import rankings feature code.

M4 reads one immutable batch using optional `modelId` and exact-hour `asOfUtc`.
Default model is `slice1-v1`; latest means greatest persisted as-of, even when
not-ready. Successful responses contain three canonical assets. There is no
model-list, available-hours, history-range, asset-detail or provider-data endpoint.
An unavailable model/hour returns 404. Do not fabricate options or infer a
continuous history. No scheduling cadence is implemented.

Scores are six-place decimal strings. Complete and qualified partial rows rank
together; not-ready rows remain present and unranked. Category inapplicability is
distinct from missing data and zero. Batch as-of, knowledge cutoff, creation and
retrieval are different concepts. Confidence scores are not probabilities.
Inapplicable category quality's stored zero is a placeholder, not poor coverage.

## What the design research supports

### 1. Organize the page around comparison and interpretation

**Documented guidance:** Carbon gives data tables generous main-content space
and treats expansion as a way to disclose supplementary information. NN/g
recommends keeping frequently needed information visible and making the path to
secondary information obvious. These are design recommendations, not universal
proof that one layout is best. [Carbon table usage](https://carbondesignsystem.com/components/data-table/usage/),
[NN/g progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/).

**Recommendation:** the first screen should answer which assets rank highest,
what model/time context applies, and whether the displayed results are usable.
Give the comparison table the main visual emphasis. Keep batch context together
above it. Offer a clearly labelled way to inspect the category/quality information
already present in the response. Exact lineage hashes belong in secondary detail.
Critical qualifications, including not-ready state and reconstructed history,
must remain discoverable without relying on a hover tooltip.

For only three assets, search, pagination, virtual scrolling, bulk selection and
a large sidebar have little demonstrated value. Adding them would be a product
decision requiring a concrete task. Avoid decorative KPI cards, unexplained
gauges, placeholder navigation and financial widgets unsupported by M4.

### 2. Build visual quality through a coherent system

**Documented guidance:** NN/g discusses scale, hierarchy, contrast and proximity;
Linear's designers describe testing alignment, density, appearance and hierarchy
across real interface states. Carbon documents typography, spacing and semantic
state colors systematically. These are useful craft references; Linear's
self-reported process is not evidence that copying its appearance improves this
app. [Visual principles](https://www.nngroup.com/articles/principles-visual-design/),
[Linear redesign](https://linear.app/now/how-we-redesigned-the-linear-ui),
[Carbon table styling](https://carbondesignsystem.com/components/data-table/style/).

**Recommendation:** develop the existing warm/teal identity. Use a small typography
scale, clear heading/body/metadata roles, aligned controls, consistent spacing,
restrained borders and purposeful surface hierarchy. Judge dense text at actual
reading size; subtle styling must not become faint text or tiny targets. Ask the
planning session to specify concrete spacing, line-height, row density, width,
radius and state tokens instead of vague adjectives such as “premium.”

Two reasonable directions to compare are a comfortable comparison workspace and
a denser comparison workspace, both within the current foundation. Choose based
on legibility, how quickly the analyst can compare columns, and narrow-screen
behavior. A redesign of the shell, a new font family or dark mode is a separate
decision, not an automatic ingredient of polish.

**Documented guidance:** shadcn recommends CSS variables and paired semantic
foreground/background tokens. [shadcn theming](https://ui.shadcn.com/docs/theming).
**Recommendation:** use named tokens for interaction states, score direction,
quality states and actual errors. A valid negative score must not look like a
failed request. Treat color as reinforcement of a sign or text label.

### 3. Make numeric comparison precise and easy to scan

**Documented guidance:** GOV.UK recommends right-aligning numbers that users
compare. Vercel's interface guidelines recommend tabular numerals and redundant
status cues. [GOV.UK tables](https://design-system.service.gov.uk/components/table/),
[Vercel interface guidelines](https://vercel.com/design/guidelines).

**Recommendation:** align numeric values and their headers, use tabular figures,
and preserve a consistent sign/decimal convention. Retain the exact transport
strings in cache. The plan should explicitly decide display precision and a
reliable, accessible way to obtain all six fractional digits. Any display
rounding is a presentation choice and must never alter ranking, sorting or stored
values. Do not use lexicographic sorting for decimal strings or silently coerce
them through floating point. Do not introduce a decimal dependency without
examining the bounded contract and justifying it.

Preserve the API's original rank even if the analyst chooses another display
sort. Explain the difference between display order and model rank; provide a
clear return to ranking order if alternate sorting is included. Keep valid zero,
missing, not-ready and inapplicable states distinct. Avoid presenting quality,
coverage and confidence as interchangeable percentages.

### 4. Treat accessibility as part of the interaction model

The target should be **WCAG 2.2 AA**, supplemented by useful stronger usability
targets. The [WCAG Recommendation](https://www.w3.org/TR/WCAG22/) is normative;
Understanding pages explain it, and APG examples illustrate patterns rather than
providing production certification.

| Area | Documented baseline | Application to evaluate |
| --- | --- | --- |
| Table semantics | Native HTML tables are preferred when appropriate; sortable headers use buttons and `aria-sort`. | Preserve the existing table structure; avoid converting a read-oriented table into an ARIA grid. |
| Text contrast | AA ordinarily requires 4.5:1; qualifying large text requires 3:1. | Measure every meaningful text/state pairing, including muted metadata. |
| Non-text contrast | Essential control/state graphics generally need 3:1 against adjacent colors, subject to the criterion's exceptions. | Assess focus/control/state indicators; decorative borders are not all required to use the same contrast. |
| Targets | AA target size is 24 by 24 CSS px or the applicable spacing/other exception. | Aim for roughly 44 px primary touch controls as a stronger usability preference; do not mislabel that as the AA minimum. |
| Focus | Keyboard operation and visible focus are required; WCAG 2.2 AA prohibits authored content from entirely obscuring focus. | Prefer fully visible focus, including with sticky headers, popovers and narrow viewports. |
| Reflow | Reflow applies at 320 CSS px; content genuinely requiring two dimensions has an exception. | Reflow controls/help normally; a contained scrollable comparison table can be appropriate. Avoid page-wide horizontal overflow. |
| Supplemental content | Hover/focus content must meet dismissal, hoverability and persistence conditions when applicable. | Essential definitions and exact values must also work with keyboard and touch. |
| Dynamic status | Status messages must be programmatically available where required; excessive live announcements can overwhelm. | Announce meaningful completion/failure, not every timestamp tick or every row. |

Sources: [APG tables](https://www.w3.org/WAI/ARIA/apg/patterns/table/),
[sortable example](https://www.w3.org/WAI/ARIA/apg/patterns/table/examples/sortable-table/),
[text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html),
[non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html),
[target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html),
[focus not obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html),
[reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html),
[hover/focus](https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html),
[status messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html).

Use more than color to communicate meaning. Respect reduced-motion preferences;
disabling nonessential interaction animation is a useful stronger target, while
WCAG's Animation from Interactions criterion itself is AAA. Do not animate score
count-ups or reorder rows theatrically. [Use of color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html),
[interaction animation](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html).

Radix supplies many keyboard/focus semantics, but application authors still own
correct names, composition, content and testing. [Radix accessibility](https://www.radix-ui.com/primitives/docs/overview/accessibility).

### 5. Design transitions, failures and refreshes as carefully as success

**Recommendation:** specify first load, same-query background refresh, refresh
failure with previous data, first-load failure, invalid input, missing model,
missing historical hour, private-use denial, schema/database unavailability,
contract/integrity failure, offline/reconnect, cancellation, partial batches and
all-not-ready batches. A missing batch is not an empty successful three-asset
response. Each state should preserve context and provide the appropriate next
action without exposing raw exception details.

Keep an existing result visible during a same-query refresh, with a quiet status.
Do not relabel old results with newly selected model/hour controls while a
different query loads. Preserve useful focus, scroll and selected-asset context.
Consider how a refreshed ranking affects someone reading a row. Scope any retained
display state carefully rather than creating a second server-data cache.

M4 has no schedule or promise of live data. A manual “Refresh rankings” action is
a sensible starting recommendation; the plan must still account for Query's
normal mount/focus/reconnect behavior. If periodic visual updates are proposed,
justify cadence and user control. WCAG's auto-update rule has no five-second
exemption for automatically updating information. [Pause, Stop, Hide](https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide).

Use explicit UTC labelling. “As of,” “knowledge cutoff,” and “retrieved” should
not collapse into an ambiguous “updated.” “Latest stored” is more accurate than
“live.” Define whether age is a fixed retrieval fact or a clearly derived display
value; a cached envelope's age does not advance automatically. Avoid invented
freshness thresholds and green “current” indicators.

Latest/exact selection should survive reload and browser back/forward via the
typed Router. Plan field-level guidance and recovery for exact UTC hours. Do not
show a calendar of “available” dates or model dropdown based on assumed records.
For richer discovery, identify an explicit later API dependency instead of
quietly expanding M4.

### 6. Measure performance under real interactions

**Documented guidance:** the good Core Web Vitals thresholds are LCP <=2.5 s,
INP <=200 ms and CLS <=0.1, evaluated at the 75th percentile of field visits.
Lab diagnostics help investigate them, but are not equivalent to field evidence.
[Threshold methodology](https://web.dev/articles/defining-core-web-vitals-thresholds),
[Web Vitals](https://web.dev/articles/vitals).

**Recommendation:** establish a production-build baseline and explicit test
device/network conditions before choosing incremental JavaScript/CSS budgets.
Measure initial route loading, direct historical navigation, input responsiveness,
sorting, opening detail and refresh under CPU/network throttling. Keep measurements
local; a private single-user app does not need an external analytics service to
claim a lab result. Do not invent p75 field results or use a Lighthouse number as
the sole release gate.

The repository already enables Router code splitting. Preserve it and inspect
which generated transport/validation code enters the rankings route. Reuse one
batch response; secondary detail should not cause per-row requests. Stable table
references and focused subscriptions are sufficient starting points for three
rows; virtualization, workers, a new state framework or pervasive memoization need
measured justification. [Router splitting](https://tanstack.com/router/latest/docs/guide/automatic-code-splitting),
[Query waterfalls](https://tanstack.com/query/latest/docs/framework/react/guides/request-waterfalls),
[Table v9 reactivity](https://tanstack.com/blog/tanstack-table-v9-reactivity),
[Table data guidance](https://tanstack.com/table/latest/docs/guide/data).

Query structural sharing and stable consumer-owned `select` projections can
limit unnecessary rendering. Validation stays in generated transport/query
boundaries. Global defaults must remain unchanged; assess any feature-specific
freshness or status-aware retry policy explicitly and document its reason.
Returning from a deterministic 403/404 should not be designed as if it were an
unbounded transient retry. [Query defaults](https://tanstack.com/query/latest/docs/framework/react/guides/important-defaults),
[render optimizations](https://tanstack.com/query/latest/docs/framework/react/guides/render-optimizations).

Profile before optimizing, especially work on the main thread during startup and
interactions. Prefer CSS flow over layout measurement loops. Reserve loading
space; avoid artificial waits and prevent repeated whole-page skeleton flashes.
[React Profiler](https://react.dev/reference/react/Profiler),
[INP optimization](https://web.dev/articles/optimize-inp).

A new font is optional. If proposed, justify its license, glyph coverage, loading
behavior and actual cost. In this private app, local assets avoid third-party
requests; self-hosting is not universally faster. Test fallbacks and layout shift
instead of assuming a `font-family` declaration guarantees consistent rendering.
[Font delivery and loading](https://web.dev/articles/font-best-practices).

### 7. Turn “production quality” into observable evidence

**Documented guidance:** Playwright recommends automated accessibility scans plus
manual assessment and inclusive user testing. Screenshot output depends on its
rendering environment. [Accessibility testing](https://playwright.dev/docs/accessibility-testing),
[visual comparisons](https://playwright.dev/docs/test-snapshots).

**Recommendation:** the later implementation should combine the existing lint,
typecheck, unit, browser and OpenAPI drift checks with explicit interaction and
visual acceptance. Test keyboard-only use, actual screen-reader workflows,
200% text enlargement, 400% zoom/reflow, reduced motion and narrow layouts. Use
deterministic synthetic fixtures and clocks for visual comparisons in the pinned
browser environment. Review the images; blindly accepting a screenshot baseline
does not demonstrate a good design. New accessibility/performance tooling should
be justified, version-resolved and pinned before installation.

Useful task-based usability checks are: identify the leading ranked asset;
interpret a partial score without treating it as missing; find the as-of and
knowledge cutoff; retrieve an exact historical hour and recover from a gap;
inspect an inapplicable category; refresh without losing reading context; and
complete these tasks with keyboard and on a narrow screen. Agree on success
criteria before implementation. A single user's feedback helps this private
product but cannot establish general population usability claims.

## Handoff to the planning session

Ask for a compact evidence review, two bounded visual directions with one
recommendation, desktop/narrow text wireframes, a field/precision presentation
matrix, a state/interaction matrix, and measurable verification criteria before
the implementation plan. The plan should resolve route/search ownership,
historical/model controls, sorting, detail disclosure, refresh/retry behavior,
responsive presentation and accessibility. It should identify exact affected
files, necessary shared-component extensions and any proposed tools, without
changing code or dependency pins during planning.

Preserve M1–M4, immutable manifests, retained private databases, same-origin
access restrictions, generated transport authority, feature ownership and Query
global defaults. No acquisition, scoring changes, backend contract expansion,
charts, watchlists/alerts, deployment, commits or pushes belong to this research.

The [pinned official shadcn skill](https://github.com/shadcn-ui/ui/blob/c257f688cf4de7ec10cc1be84cad29cd4631182c/skills/shadcn/SKILL.md)
was read. Apply its relevant composition/accessibility/token guidance under the
repository's more specific rules: local CLI 4.21.0, `@shadcn`, new-york/Radix,
existing customizations and `react-icons/lu`. Its generic dashboard recipe does
not require a sidebar or chart here. Official [Radix Table](https://ui.shadcn.com/docs/components/radix/table)
and [Button](https://ui.shadcn.com/docs/components/radix/button) docs were inspected;
no components were installed. Some web-reader URLs required the official raw
GitHub/CLI fallback; CLI inspection succeeded with network access. No application
file, active execution plan, dependency or image pin was changed for this research.
