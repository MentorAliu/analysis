# Scoring model

**Status:** Requirement for structure and language. Weights, thresholds, and formulas are provisional until backtested.

This is the analytical framework. Pipeline execution is in [data-pipeline.md](data-pipeline.md). Concepts are in [domain-model.md](domain-model.md).

**Implemented M3:** [slice1-v1](../../backend/src/Analysis.Domain/Scoring/Manifests/README.md)
is the concrete 21-definition BTC/ETH/SOL subset. Its manifest is the sole numerical
specification; broader families/examples below are not extra implemented features.
Explicit T/K input capture, immutable lineage, qualified partial scores and local
replay are implemented. Regime is a category for alts, inapplicable for BTC.
Normalization uses fixed thresholds, not three-asset peer percentiles. Quality
measures usable directional weight; context coverage is separate and provider
agreement remains unassessed with single sources. Predictive validation is future.

## Purpose

Turn normalized features into inspectable, reproducible ranks. The first output is a **composite score** plus **bullish confidence** and **bearish confidence** scores. These are heuristic constructs. They are not probabilities of profit and not expected returns.

## Layers

Keep these distinct:

| Layer | What it is | Example |
| --- | --- | --- |
| Raw metric / observation | Measured fact | Perpetual funding rate at time T |
| Derived feature | Deterministic transform | 24h change in funding; funding vs 30d median |
| Category score | Weighted/normalized summary of one family | Derivatives category score |
| Composite score | Summary of category scores | Asset rank input |

Natural-language interpretations are a fifth layer. They never feed back as numeric inputs. An LLM-extracted catalyst may become a separately validated, provenance-bearing structured event; deterministic code may then derive an explicit feature from that event.

## Signal families

The architecture must support these families over time. MVP implements a subset; see [../exec-plans/active/first-ranking-vertical-slice.md](../exec-plans/active/first-ranking-vertical-slice.md).

1. **Market regime** — BTC trend and structure, BTC dominance, ETH/BTC, broad crypto/alt conditions, stablecoin liquidity, macro and financial conditions.
2. **Price structure and relative strength** — trend, support/resistance, breakouts/breakdowns/rejections/retests, momentum, relative strength vs BTC, ETH, and sector peers.
3. **Spot / order flow** — spot volume, spot CVD, CVD divergences, order-book depth, bid/ask imbalance, liquidity gaps, slippage.
4. **Derivatives** — open interest, OI change, OI vs market cap, funding, futures basis, futures vs spot activity, liquidations, liquidation clusters, long/short positioning, options metrics where liquid enough.
5. **On-chain / capital flows** — exchange inflows/outflows, reserves/netflows, whale accumulation/distribution, insider/VC/foundation/treasury wallets, post-unlock token movements, holder concentration, cost-basis metrics where useful.
6. **Tokenomics** — circulating and total supply, FDV, unlocks, emissions, inflation, staking issuance, burns, buybacks, treasury accumulation.
7. **Fundamentals** — TVL, fees, revenue, users, transactions, DEX/perpetual activity, ecosystem growth, integrations, institutional/enterprise adoption, development progress.
8. **Token value capture** — required token usage, staking demand, fee payments, buybacks, burns, reserve accumulation, distributions to holders.
9. **Catalysts and risks** — upgrades, listings, governance, partnerships, institutional developments, regulatory/legal, security incidents.
10. **Secondary technical indicators** — RSI, MACD, moving averages, Bollinger Bands, ATR, stochastic, VWAP.

**Requirement:** family 10 is confirmation, not the primary foundation.

## Normalization principles

- Convert features to a comparable scale **inside a documented function** (ranks, z-scores, clipped min-max, or regime buckets). The choice is part of the model version.
- Normalize within a defined peer set (for example, Stage B universe as of T). Do not mix BTC dominance with a microcap’s 5-minute RSI on an implicit shared 0–100 scale without a defined transform.
- Winsorize or clip extreme values with documented bounds. Do not let a single exploded feature dominate a category unless the model explicitly wants that.
- Preserve direction before averaging. Do not average mixed-direction features without sign alignment.

## Directionality

Every feature must declare direction relative to the bullish axis of its category, for example:

- Higher 30d relative strength vs BTC → more bullish in price-structure (unless the regime model says otherwise).
- Higher perpetual funding → crowded long; the derivatives category may treat that as **risk / mean-reversion pressure**, not as bullish momentum.

Direction is a model decision, recorded in the manifest. Silent sign errors are a first-class testing risk.

Bearish confidence is not automatically `1 - bullish confidence`. Crowded, high-data-quality markets can produce high confidence in both a trend and a squeeze risk. The model may output both scores from different feature groups. Document the actual functions in the model version; do not assume a complement relationship.

## Missing data, applicability, and quality

| Case | Behavior |
| --- | --- |
| Inapplicable (no options market) | Omit from the category; do not score as 0 |
| Temporarily missing | Lower data-quality / confidence; skip or carry last good value only if the manifest allows a stale window |
| Stale beyond policy | Treat as missing; flag freshness |
| Conflicting providers | Do not average silently; use a documented primary source or quarantine |

A **data-quality** or **coverage** indicator must travel with scores so the UI can distinguish “neutral because mixed evidence” from “neutral because we lack data”.

## Divergence detection

Divergences are features or signal flags, not a hidden adjustment to the composite.

Examples: price making higher highs while spot CVD or OI does not; funding elevated while price stalls.

Record the two series, the window, and the rule. Divergences may influence a category score only through those explicit features.

## Why scores must be deterministic

Analysts need to replay Tuesday’s ranking next quarter. Tests need golden vectors. Backtests need a stable mapping from inputs to outputs.

Therefore:

- No wall-clock randomness in scoring.
- No hidden reads of “latest” data without an as-of time.
- No LLM or remote model as the numeric calculator.
- Same feature set + same manifest + same as-of observations = same scores.

## Score versioning

A **ScoringModelVersion** includes:

- Model id and immutable version string
- Feature ids and calculation versions
- Normalization methods and peer-set rule
- Category weights and composite combination rule
- Missing/inapplicable/stale policies
- Direction map
- Hash or serialized manifest stored with each score

Weight or policy changes create a new version. Historical rows keep the old version.

Each score record references this immutable model version and the exact feature snapshot (or immutable feature-value identifiers) consumed. A model id without input lineage is insufficient for replay.

## Score-change explanations

Explanations are generated from structured differences between two score snapshots:

- Compare like-for-like model versions when describing market-driven change.
- If the model version changed, separate model-driven change from input-driven change.
- Identify feature changes, category contributions, coverage/freshness changes, and relevant dated events.
- Preserve the referenced “before” and “after” snapshot ids.

Natural-language rendering may summarize this diff, but it must not introduce unsupported causes.

## Initial weighting philosophy

**Provisional.** Not estimated from a study. Not to be presented as calibrated.

Until backtests exist:

- Weight categories that the slice actually measures. Do not give empty families a non-zero weight.
- Prefer a small number of high-value features over a kitchen-sink average.
- Keep secondary technicals at low weight or outside the composite (confirmation only).
- Keep catalysts as event overlays and explanation inputs until they have stable structured features.
- Treat BTC-centric regime as context that can dampen or scale alt scores rather than as one more equal-sized token feature.

Example **provisional** MVP stance (replace after evidence):

- Price structure / relative strength: material
- Derivatives: material
- Market regime: material as a context scalar or category
- Fundamentals: present only when applicable (ETH, SOL more than BTC)
- Other families: weight 0 until ingested

Do not treat the relative sizes above as constants to encode without listing them in a versioned manifest.

## Confidence versus composite

- **Composite score:** where the asset sits on a signed bullish/bearish ranking axis for this model version.
- **Bullish confidence score:** how concentrated and high-quality the bullish evidence is.
- **Bearish confidence score:** how concentrated and high-quality the bearish evidence is.
- **Data quality:** coverage, freshness, provider agreement — not a directional bet.

These may correlate; they are not the same number.

## Why probabilities require calibration

A 0–100 heuristic can be useful for ranking and still be miscalibrated (70 ≠ 70% chance of a positive 7d return).

Probability language is allowed only after a documented evaluation: defined horizon, defined return series, defined universe, defined model version, and out-of-sample evidence. Until then, UI and APIs use score/confidence terminology from [../product/product-spec.md](../product/product-spec.md).

## LLM role

**Future:** generate explanations from structured score diffs; extract catalyst candidates from text.

**Requirement:** LLMs must not emit the official composite, category, or confidence numbers.

## Unresolved

- Predictive suitability of the frozen M3 formulas, windows and weights; later versions require evidence and a new plan.
- Peer-set definition for cross-sectional normalization once the universe grows.
- Whether a later validated regime model should replace M3's explicit category.
- Calibration method and probability mapping, deferred until outcomes exist.
