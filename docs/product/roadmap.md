# Product roadmap

**Updated:** 2026-09-05.

**Status:** Requirement for product sequencing and promotion gates. Feature designs marked **Proposed** remain subject to a subsequent execution plan; **Future** items are conditional. Inclusion is not evidence of implementation, predictive performance, or customer demand.

The [product specification](product-spec.md) defines the product boundary. The [active first-ranking plan](../exec-plans/active/first-ranking-vertical-slice.md) remains the implementation authority for the current slice. This roadmap adds subsequent work without expanding that slice. No delivery dates are committed.

## Product objective

Help analysts preserve why an asset deserves attention, identify meaningful changes in that evidence, and later review whether the original signals were useful. Test whether this recurring workflow supports paid subscriptions before expanding the feature catalog.

Priorities follow the [feature research](../research/feature-research-2026-09-05.md) and [product and income assessment](../research/product-and-income-assessment-2026-09-05.md). The research establishes relevant problems and competing capabilities; it does not establish willingness to pay or guarantee primary income.

## Delivery order

| Order | Roadmap item | Status | Exit or promotion gate |
| --- | --- | --- | --- |
| 0 | BTC/ETH/SOL technical ranking slice | Requirement; M1 complete; M2 private-use ingestion verified for Kosovo; M3–M5 not started | Complete M3–M5 and the active plan's remaining acceptance checks. Review commercial data rights before sharing/monetization. |
| 1A | Forward signal recording and outcome collection | Proposed; first work after the slice | Reproducible original records, predefined outcome conventions and benchmarks, and passing integrity checks. |
| 1B | Asset detail with precise change explanations | Requirement for the planned explanation workflow; after the slice | Correctly distinguish asset, peer/universe, model and data-quality changes. |
| 2 | Saved research thesis monitor | Proposed; first customer-facing paid-workflow experiment | Correct reviews, useful repeat usage and evidence of paid renewal under a predefined pilot protocol. |
| 3 | Forward signal record with benchmark comparisons | Proposed; display as observation horizons mature | Complete and incomplete results remain visible; aggregates disclose sample, coverage and model version. |
| Later | Comparable token economics | Future; conditional | A validated customer task, comparable coverage and confirmed commercial data rights. |
| Later | Supply-event revisions and uncertainty monitoring | Future; conditional | Relevant assets beyond the initial universe, validated demand and confirmed commercial data rights. |

1A and 1B are prerequisites for a credible pilot. Outcome collection continues while the thesis workflow is tested; stage 3 need not wait for completion of that pilot. A mature timestamp alone is insufficient evidence of predictive usefulness.

## 0 — Complete the technical slice

Execute the [existing plan](../exec-plans/active/first-ranking-vertical-slice.md) in order, beginning with M1. Its BTC/ETH/SOL universe, approved feature scope and basic ranking table remain the acceptance boundary. M3 already requires immutable score versions and exact feature-input lineage.

Outcome jobs, asset-detail expansion, saved theses, alerts and customer identity belong to subsequent execution plans. Do not present reconstructed records from the technical slice as signals originally published to customers.

## 1A — Preserve original signals and collect outcomes

**Proposed:** extend existing immutable score history into a forward record of issued rankings. Capture publication time separately from logical as-of time, exact input snapshot, model version, eligible universe, quality state and a defined reference-price convention.

Before evaluating results, freeze the selected horizons, price series and quote units, missing-data policy, benchmark definitions and aggregation rules. The product specification's outcome horizons remain the target set; a subsequent plan must explicitly select its initial subset. Evaluate a market benchmark and a simple relative-strength ordering as proposed comparisons, without inventing their definitions or results here.

Attach returns and, when covered by the subsequent plan, MFE/MAE only after the required observations exist. Jobs must be idempotent and replayable. Retain poor, pending and incomplete results; distinguish originally issued signals from backfilled research. Do not silently replace original inputs with revised provider history.

**Gate:** fixture-based checks demonstrate time isolation, price/quote correctness, maturity boundaries, missing observations, duplicate runs and preservation of original records. BTC/ETH/SOL can validate this pipeline; three assets cannot establish broad cross-sectional ranking skill.

## 1B — Explain changes accurately

**Requirement for planned asset detail:** compare explicit before/after snapshots and identify:

- Changes in the asset's own observations, features and category scores.
- Changes in peers or universe membership, including their effect on normalization and rank.
- Model-version changes.
- Missing, stale, conflicting or newly available data.

A higher rank caused by falling peers must not be described as improving asset fundamentals. Stale evidence must not be presented as a failed market condition. Where nonlinear scoring prevents an exact additive breakdown, disclose the attribution method and limitations.

**Gate:** deterministic cases cover unchanged asset inputs with falling peers, a new peer, a model revision, a stale input and a true feature change. Basic explanations and quality disclosures are core product behavior, not an optional paid accuracy feature.

## 2 — Test a saved research thesis monitor

**Proposed:** let a user save an asset, research horizon, reasons for watching it, supporting/opposing evidence and explicit review conditions. Preserve the original evidence and version subsequent edits to notes and conditions.

Start with approved market and derivatives features, one hypothesis per research item and one notification channel. Evaluate structured conditions deterministically. Distinguish met, not met, conflicting evidence and not evaluable; a condition's state is not an objective verdict on an investment thesis. Free text remains user interpretation, and an LLM does not decide the state or calculate scores.

Show meaningful state transitions with before/after values, provenance and timestamps. Deduplicate repeated notifications and provide user-controlled cadence. Allow users to record their review so they can resume their reasoning later.

**Unresolved before implementation:** customer identity and access isolation, the initial channel, supported condition operators, cadence semantics and retention policy. Record these in the subsequent execution plan; do not build a general rule engine or arbitrary news interpretation for the pilot.

**Commercial gate:** compare the prototype with the selected analyst's existing workflow using matched cases, including meaningful deterioration, harmless fluctuation and missing data. Measure time to a correct review, missed changes, unnecessary interruptions, repeated use and paid renewal. Record recruitment criteria, observation period, success thresholds and price before the pilot. These are unresolved experiment settings, not fabricated validation results. If value or renewal is weak, revise the workflow before widening coverage.

## 3 — Publish an inspectable benchmark record

**Proposed:** expose original rankings and matured outcomes with benchmark-relative comparisons. Show periods, sample sizes, coverage, model versions and pending/incomplete observations. Preserve unfavorable results and separate backfilled research from originally issued output.

Overlapping horizons and repeated signals on one asset are not independent samples. Do not select the best-looking horizon after observing results or label observational returns as realized trading profit. Heuristic scores remain non-probabilistic. Independent verification claims require an independent mechanism; an operator-controlled append-only database alone is insufficient.

**Proposed packaging:** basic quality and methodology disclosures stay accessible; saved research monitoring and advanced comparisons are candidates for a subscription. Pricing and conversion remain unresolved until tested.

## Conditional expansion

**Future — Comparable token economics:** compare appropriately similar protocols and periods, separating fees, protocol revenue, holder distributions, executed buybacks/burns, incentives, issuance and scheduled releases. Preserve protocol-to-token identity and announced/approved/executed states. Prevent double counting of the same flow. Do not force BTC, blockchains and DeFi applications into one universal income statement.

**Future — Supply-event revisions and uncertainty:** preserve original and revised schedules, sources, last-checked times, date precision, recipient categories and explicit supply denominators. Distinguish scheduled releases from observed claims, transfers, issuance and burns. Unlocks and exchange transfers are not proof of selling or predicted price impact.

Promote either item only after research demonstrates a recurring task beyond existing alternatives, official provider documentation confirms applicable coverage, and commercial display/storage rights and costs are resolved. Neither adds inputs to the composite without a separately specified scoring-model version and validation.

The broad Stage A/Stage B scanner, additional signal families and qualitative catalysts remain in the [product specification](product-spec.md). Expand the universe when the selected customer workflow and data coverage justify it. Calibration remains conditional on historical evidence. Generic chat, a larger indicator catalog and standalone calendars are deferred relative to the priorities above.

## Execution rule

Application work follows a bounded active execution plan with concrete scope, data contracts, dependencies and acceptance checks under the [testing strategy](../engineering/testing-strategy.md). Update relevant design documents when that plan resolves implementation decisions. Roadmap placement does not authorize trading, custody, probability claims or a change in architecture.
