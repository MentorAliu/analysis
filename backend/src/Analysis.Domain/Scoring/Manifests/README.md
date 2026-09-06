# Deterministic M3 model contract

`slice1-v1.json` is the sole numerical configuration for this provisional model.
Feature operations are implemented by `FeatureCalculator`; `ScoreCalculator`
expands its category/group weights. The immutable manifest includes applicability,
units, horizons, transforms, thresholds, policy and calculation-version identifiers.
Do not copy its constants into another configuration or silently modify a version
already stored in a retained database. These are engineering choices, not validated
predictors, expected returns or probabilities. See the [active plan](../../../../../docs/exec-plans/active/first-ranking-vertical-slice.md).

## Operation semantics

`T` is an explicit UTC hour boundary and `K` an explicit knowledge cutoff. A candle
timestamp is its opening time; `C(T)` means the close of the candle ending at `T`.
Only closed candles and scalar events at/before `T`, ingested at/before `K`, qualify.
All applicable windows must be complete under the manifest's history policies.

| Operation | Exact meaning (horizon `h` comes from the manifest) |
| --- | --- |
| close | `C(T)` |
| quote-volume | Sum quote volume of the last `h` closed candles |
| quote-volume-change | Current `h`-bar quote volume / preceding `h`-bar volume, minus one |
| return | `C(T) / C(T-h) - 1`, including every intervening candle |
| realized-volatility | Square root of sum of squared hourly simple returns over `h`; unannualized |
| relative-strength | `(1 + asset return) / (1 + BTC return) - 1`, over the same horizon |
| moving-average-distance | `C(T) / arithmetic mean(last h closes) - 1` |
| btc-return | BTC simple return over `h` |
| funding-last | Most recent actual settlement within the age policy |
| funding-sum | Sum actual settlements in `(T-h,T]` |
| funding-change | Current settlement sum minus preceding equal-window sum |
| oi | Both-sides base-unit observation at exact `T` |
| oi-change | Exact current/anchor OI ratio minus one, with contiguous hourly samples |
| tvl | Latest eligible timestamped USD chain observation at/before `T` |
| tvl-change | Latest eligible current/anchor TVL ratio minus one |

Funding requires a predecessor at/before the window start, within the manifest's
gap policy. Boundary and internal gaps are checked using observed settlement
times; metadata's current interval never implies a historical schedule. An empty
window is unavailable. TVL anchors must be distinct; both anchor ages and all
intervening gaps are checked. Its actual endpoint timestamps and elapsed duration
are retained, without an invented daily-close time.

Every feature stores its state/reason, units, exact input keys, relevant conflict
IDs and checked windows, including failed windows. BTC's inapplicable features
remain explicit. Core price readiness independently checks the entire longest
return window. `missing`, `stale`, `invalid` and `conflicted` cannot become a zero
measurement. A valid measured zero remains available. Raw observation validation
retains M2's precision, canonical identity and explicit unit rules.

## Score and numeric conventions

`clip` divides by the manifest threshold and clamps to the signed unit interval.
`negative-clip` reverses its sign. `oi-confirmation` multiplies the matching clipped
price return by capped positive OI growth relative to its threshold; contraction
is neutral. Context operations never directly enter directional scores.

Leaf weights are stored as exact integer numerators over the manifest denominator;
applicable weights sum exactly to that denominator. Available-weight means yield
category/composite scores. Quality is usable directional mass over applicable
mass. Bullish/bearish confidence sums positive/negative evidence against the full
applicable mass independently; missing evidence therefore reduces confidence.
Effective available-weight fractions are retained exactly through each usable
evidence numerator and the stored category available-weight totals (sum these
totals for the composite denominator), avoiding rounded repeating weight decimals.
Final values are emitted only if core price history and the manifest's quality
gate pass. Neutral complete evidence has zero composite and both confidences zero.
Context coverage is separate; missing applicable context marks an otherwise ready
score partial. Single-source provider agreement is explicitly unassessed.

`decimal18-v1` uses checked .NET decimal arithmetic and fixed feature-ID reduction
order. Round each division, completed feature and normalized value with the
manifest's decimal places and midpoint rule; final scores use its score precision.
Intermediate products use decimal's representable precision, with checked
overflow. Never use binary floating point or silently coerce unsupported raw
magnitudes. For square root, recover the exact decimal integer coefficient `c`
and scale `q`. At output precision `p`, integer-square-root `c*10^(2p-q)`; compare
four times that radicand with `(2*floor+1)^2` to round to nearest, ties to even.
Convert the scaled result through the existing exact decimal validator.

Canonical JSON sorts object properties ordinally, preserves array order, uses
UTF-8 without whitespace, and canonicalizes JSON numeric literals. Typed internal
numbers use exact decimal/integer strings; these are persistence documents, not
an HTTP transport contract. SHA-256 identifies canonical manifest/input/feature/
score content. The calculator source digest hashes sorted embedded Domain source
names and LF-normalized text, including existing observation/time/numeric rules.
Published Domain resources retain the exact replay implementation. A hash or
source mismatch fails replay; changed behavior requires a newly registered version.

## Persistence and replay

The additive EF migration owns model versions, fixed-universe scoring batches,
frozen input observations/conflicts, feature snapshots/values and score/category
snapshots. Batch inputs include all canonical observations in the bounded capture
window; individual feature details identify the subset and failed-window evidence.
Relational input links reference M2 observation composite keys and quarantine IDs;
payload/mapping hashes remain reachable through those existing facts.

Capture uses read-only PostgreSQL Repeatable Read. Calculate from that frozen
capture, then publish the whole universe in one separate transaction using model
and model/as-of advisory locks. The model registration lock also serializes
different hours for this small slice. Duplicate keys reuse the winning stored
bundle; a changed cutoff conflicts. Composite foreign keys bind score asset,
as-of/model and feature snapshot together. Deferred completeness guards reject
partial publication. Children may be inserted only in the batch's creating
transaction. UPDATE, DELETE and TRUNCATE are rejected for every M3 table.

Replay is read-only and verifies hashes, relational lineage, original M2 facts
and payload bytes, then recomputes every feature/category/output. Later arrivals
do not refresh the snapshot. These are historical research reconstructions:
creation time and `K` can follow `T`; they are not contemporaneously issued signals.
The immutability guards protect normal SQL operations, not a database owner who
deliberately disables triggers or edits migration history.

## Verification

`Analysis.ScoringChecks` is a package-free executable with synthetic inputs and
independently calculated score vectors. `scripts/verify-m3.mjs` uses disposable
PostgreSQL for upgrade/down/reapply, immutable writes, concurrency, cancellation,
replay and recreation tests. Production API/worker images exclude check assemblies
and fixtures. `scripts/verify-m3-private.mjs` is a separately authorized one-batch
acceptance tool; its durable ignored claim prevents automatic repeated acquisition.
It performs later scoring/replay on the internal-only database network. See the
active plan for actual evidence, private access limitations and completion status.
