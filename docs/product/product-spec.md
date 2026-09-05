# Product specification

**Status:** Requirement, unless marked otherwise.

Delivery priorities and promotion gates are maintained in the [product roadmap](roadmap.md). The active execution plan defines the current implementation scope.

## Purpose

Help a crypto analyst continuously see which liquid assets deserve attention, why they rank where they do, and whether historical signals were useful after the fact.

The product is a research and analytics workspace. It is not a broker, exchange, or wallet.

## Target user

Primary user: a discretionary or systematic crypto analyst who already understands market structure and wants a consistent, inspectable ranking of opportunities.

Secondary users (**Future**): portfolio assistants who consume rankings and explanations but do not configure scoring models.

The user is not an end-retail trader looking for one-click buy buttons.

## Problem

Liquid crypto markets produce more data than a person can watch: spot, derivatives, on-chain, tokenomics, fundamentals, and catalysts. Ad-hoc dashboards mix vendor screenshots, unversioned indicators, and gut feel. Analysts cannot reconstruct why a score changed last Tuesday, or measure whether “bullish” actually preceded positive returns.

This product exists to:

1. Scan a broad liquid universe cheaply.
2. Analyze qualified assets deeply.
3. Rank them with a deterministic, versioned score.
4. Explain score changes from structured inputs.
5. Store history and later measure outcomes.

## Key workflows

### Market scanner

Stage A filters a broad universe with inexpensive eligibility and quality checks (liquidity, market cap, volume, spread, exchange coverage, asset age, basic momentum/relative strength, data availability). Illiquid microcaps, manipulation-prone assets, and assets without reliable data are excluded.

Stage B runs expensive analysis only on qualified candidates.

The scanner answers: “which assets are even eligible, and which of those look interesting enough to inspect?”

Details: [data-pipeline.md](../design/data-pipeline.md), [scoring-model.md](../design/scoring-model.md).

### Asset detail analysis

An asset page shows the current composite and category scores, the contributing features, market/derivatives context, applicable fundamentals, and dated events. Secondary technical indicators are confirmation, not the headline.

The page must make the score inspectable: raw observation → feature → category → composite.

### Rankings

A ranking table lists qualified assets by composite score and supporting category scores, with data-quality/confidence context and last-updated time. Filters persist in the URL (TanStack Router search params).

Rankings are research orderings, not trade recommendations and not probabilities.

### Score-change explanations

When a composite or category score changes, the product shows what changed in the structured inputs: which features moved, which categories moved, missing-data or quality shifts, and any new catalyst/unlock event attached to the as-of time.

The planned asset detail must distinguish changes in the asset's own inputs from peer/universe movement, model revisions and data-quality changes, preserving before/after snapshot references. A rank improvement caused by declining peers is not evidence that the asset itself improved. Nonlinear attribution must disclose its method and limitations rather than invent exact additive contributions.

Natural-language explanation is a rendering of those structured diffs. It is not a substitute for the numeric model. **Future:** LLM-written narrative over the same structured diff.

### Watchlists and alerts

**Proposed, after the first vertical slice:** users pin assets and receive alerts when scores, categories, or named events cross thresholds. The first customer-facing experiment is a saved research thesis monitor: record why an asset matters, supporting/opposing evidence, a research horizon and explicit review conditions, then inspect meaningful changes and a versioned review history.

Structured conditions are evaluated deterministically, with met, not met, conflicting and not-evaluable states. Missing or stale evidence must not masquerade as a failed market condition. Begin with already-approved market/derivatives features and one notification channel; deduplicate repeated triggers and allow user-controlled cadence. See the [roadmap's pilot gate](roadmap.md#2--test-a-saved-research-thesis-monitor).

**Unresolved:** alert channels (in-app only, email, webhook) and authentication/identity.

### Historical signal analysis

Every persisted score/signal should eventually be joinable to subsequent returns and path metrics: 1h, 4h, 1d, 3d, 7d, 14d, 30d, maximum favorable excursion (MFE), and maximum adverse excursion (MAE).

**Proposed priority:** begin forward outcome collection in a subsequent execution plan immediately after the technical slice, and expose benchmark comparisons as horizons mature. Preserve issued time separately from as-of time, exact input/model/universe references and data-quality state. Distinguish originally issued signals from reconstructed research. Freeze outcome and benchmark conventions before evaluation; show unfavorable, pending and incomplete records, sample sizes and coverage. Observational returns are not realized trading profits, and overlapping horizons are not independent evidence.

This workflow answers: “did this scoring-model version have any relationship to later returns, or was it noise?”

Do not describe uncalibrated scores as statistical probabilities. Use **composite score**, **bullish confidence score**, and **bearish confidence score** until calibration exists. See [scoring-model.md](../design/scoring-model.md).

## MVP versus later

### MVP (first vertical slice)

Proved by [../exec-plans/active/first-ranking-vertical-slice.md](../exec-plans/active/first-ranking-vertical-slice.md):

- Universe of BTC, ETH, and SOL.
- Basic market data, derivatives, and applicable protocol fundamentals.
- Approximately 15–25 high-value features.
- Deterministic, versioned composite score persisted with lineage.
- `GET` rankings endpoint.
- Basic ranking dashboard.

The MVP proves the pipeline, not the full indicator catalog.

### Later product functionality

Sequencing and validation gates are in [roadmap.md](roadmap.md); these items are not additions to the active technical slice.

- Two-stage scanner over a broad liquid universe.
- Full signal families in [scoring-model.md](../design/scoring-model.md).
- Asset detail with precise score/rank-change explanations and data-quality distinctions.
- Watchlists and alerts, with a **Proposed** saved research thesis monitor as the first paid-workflow experiment.
- Forward historical outcome collection, followed by an inspectable signal record with benchmark and model comparisons.
- **Future, conditional:** comparable token economics and supply-event revision/uncertainty monitoring, subject to validated customer tasks and commercial data rights.
- Qualitative catalyst extraction (LLM-assisted, not score-authoritative).
- Calibrated probability estimates, only after evidence exists.

## Non-goals

Do not implement:

- Exchange trading, order placement, smart-order routing, or “copy trade”.
- Custody, deposit addresses, wallet connect as a funding path, or withdrawals.
- Claiming that heuristic scores are win probabilities or expected value.
- Calculating every expensive indicator for every token in existence.
- A social feed, copy-trading marketplace, or token-issuance platform.
- Microservices, Kafka, or Kubernetes as a prerequisite for the product.

## Language rules

| Allowed now | Not allowed until calibration |
| --- | --- |
| Composite score | Probability of profit |
| Bullish confidence score | Win rate presented as a forecast |
| Bearish confidence score | Statistical edge claims without sample and model version |

UI copy must keep this distinction even if a score is scaled 0–100.
