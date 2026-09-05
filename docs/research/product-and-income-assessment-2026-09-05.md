Repository and income assessment — 5 September 2026

Status: analysis and proposed business experiments. This report does not change the product requirements, scoring model, provider selection, or active execution plan. Prices and calculations are dated observations or explicitly stated assumptions. Customer needs described below are hypotheses to validate, not findings from customer interviews.

**My assessment is that this could support a focused subscription business, but the repository does not yet establish that it can generate an income.** It contains a coherent engineering blueprint for crypto research. The commercial opportunity is to make a recurring research task easier, faster, and more trustworthy. Whether enough customers will pay for that remains the central unanswered question.

I would pursue a narrow product for experienced independent analysts who monitor liquid crypto assets over days to weeks. Its main job would be to show which assets warrant investigation, what changed since the user's last review, and what evidence weakens each apparent opportunity. I would test that workflow with paying users before funding the entire ten-family analytics roadmap.

**What is actually in the repository.** I read all nine tracked files at commit `bec9e26` (`docs: establish crypto platform baseline`). The checkout was clean on `main`, with no local ahead/behind difference reported against the existing `origin/main` tracking reference; I did not fetch or independently verify the live remote. All tracked files are Markdown documents. There is no frontend, backend, dependency manifest, migration, Compose configuration, automated test, or README. The active plan explicitly says implementation has not started. Consequently, this is a design and business assessment, not a review of a running application's code, security, performance, or visual experience.

| Document | What it establishes | Practical implication |
| --- | --- | --- |
| [Agent contract](../../AGENTS.md) | Analytics only, deterministic scores, UTC, canonical identity, execution-plan boundaries | A clear scope that avoids drifting into exchange trading or custody |
| [Product specification](../product/product-spec.md) | Screening, ranking, explanations, history, and later alerts | Defines the intended research workflow, but has no pricing or customer validation |
| [Architecture](../../ARCHITECTURE.md) | React/Vite frontend; ASP.NET Core API and worker; PostgreSQL and Redis | A proposed modular monolith that is sufficient for this scope without more infrastructure |
| [Domain model](../design/domain-model.md) | Assets, observations, features, scores, signals, interpretations, outcomes | Separates measured facts from calculations and explanatory text |
| [Data pipeline](../design/data-pipeline.md) | Adapter mapping, scheduling, precision, timestamps, retries, replay | Treats data correctness as a product requirement |
| [Scoring model](../design/scoring-model.md) | Ten eventual signal families; versioning, directionality, missing-data rules | Defines score semantics, but no implemented or validated predictive model |
| [Data sources](../engineering/data-sources.md) | Required capabilities and commercial licensing criteria | No vendor, usable license, coverage matrix, or operating data bill is established |
| [Testing strategy](../engineering/testing-strategy.md) | Mapping, golden vectors, replay, API contract, integration, financial-data tests | A strong correctness plan; no executed results or predictive-performance study |
| [First vertical slice](../exec-plans/active/first-ranking-vertical-slice.md) | BTC, ETH, SOL; 15–25 features; persisted scores; rankings endpoint and dashboard | A useful technical proof, with alerts, auth, outcome jobs, and broader scanning excluded |

**The intended product is a research prioritization system.** It should continuously observe a liquid crypto universe, apply inexpensive eligibility checks, calculate deeper analysis for qualified assets, rank them consistently, and preserve enough history to explain and evaluate its own outputs. In ordinary language, it should help someone answer four questions: what deserves my attention, why, what changed, and how useful were previous observations?

The planned flow is external data → provider adapters → normalized observations → derived features → category scores → composite scores → saved rankings/signals → dashboard → later outcome measurement. Workers perform and persist calculations; the API reads the saved results. An LLM may eventually explain structured changes or propose catalyst events, but cannot calculate official scores. These distinctions are explicit in the source documents.

The broad vision includes price structure, market regime, derivatives, order flow, on-chain flows, tokenomics, fundamentals, token value capture, catalysts, and supporting technical indicators. The first implementation covers only a small subset for three assets. The product should never imply that a heuristic score of 70 means a 70% chance of profit.

**The human need is less fragmented research and better control of attention.** A useful product should reduce repeated checking, make uncertainty visible, and give users a defensible record of what they knew at the time. More numbers do not automatically accomplish that. My proposed customer and workflow assumptions are:

| Potential customer | Recurring task | Evidence that would indicate willingness to pay | Initial priority |
| --- | --- | --- | --- |
| Experienced independent discretionary analyst | Review a liquid-asset watchlist and investigate meaningful changes | Already pays for research, maintains a manual routine, and renews a paid pilot | First segment to test |
| Small research team or independent research publisher | Prepare recurring briefs and document the reasoning behind them | Pays for shared research history and clearly licensed report use | Secondary experiment if accessible to the founder |
| Systematic researcher | Obtain reproducible historical features and test models | Requires dependable exports, point-in-time data, coverage and licensing | Later; materially different product expectations |
| Casual crypto observer | Check prices or general market news occasionally | Would need to demonstrate a recurring unmet task despite existing tools | Low initial priority |

The current specification groups discretionary and systematic analysts together. I would choose one first: their requirements diverge. A discretionary analyst needs rapid interpretation and useful interruptions. A systematic researcher needs complete historical datasets, export rights, and machine-readable contracts. Building both immediately would expand support and data costs before either segment is validated.

The initial positioning I would test is: “Review the meaningful changes across your liquid-crypto watchlist in one brief, with supporting evidence, counterevidence, and a timestamped history.” A claim such as “finish your review in ten minutes” should remain a usability target until observed in real sessions. Do not market time saved, superior returns, or avoided losses as established results.

**The first screen should reflect a daily routine.** I would prioritize changes since the last visit, a small research shortlist, and the user's watchlist. A user should be able to open an asset, inspect the main supporting and opposing inputs, see their timestamps, and save a research note. The next relevant change should reach them through one chosen notification channel. The system should also report when there is no material change.

Illustrative copy using hypothetical inputs, not a current market signal: “ETH's research rank improved as relative strength increased. Funding also became more crowded, which adds conflicting evidence. The derivatives input is older than the price input.” This explains what the system observed without pretending to know why a market price moved or what a user should buy.

Useful notifications should have deduplication, a material-change rule, a cooldown, a digest option, and a clear reason for arrival. Users need control over interruption frequency. A high ranking should not become an urgent notification by default. Freshness, methodology, data limitations, and counterevidence should remain visible wherever a score is shown, including free samples.

**Competition makes focus essential.** These are current observations from official product pages checked on 5 September 2026. Listed features are vendor descriptions, not independently verified investment-performance claims. Consumer subscriptions and commercial data licenses are different products.

| Alternative | Observed offer or capability | Implication for this product |
| --- | --- | --- |
| Nansen Pro | $69/month on monthly billing, or $49/month billed annually; on-chain analysis, labels and smart alerts | A broad premium analytics product already competes around this subscription range. [Official plan description](https://academy.nansen.ai/articles/9412804-about-nansen-pro) |
| Santiment Sanbase Pro | Advertised at $49/month; current and historical analytics, screeners and alerts | Combining data sources, screening and notifications is already an established offer. [Official pricing](https://app.santiment.net/pricing) |
| Glassnode Advanced | $49/month billed annually for personal research; Market Compass provides a daily composite and analyst interpretation | “A score plus explanation” is already competitive territory. [Pricing](https://studio.glassnode.com/pricing), [Market Compass](https://glassnode.com/products/studio/market-compass) |
| CoinGlass | Its public market table presents funding, open interest, volume and liquidation context | A dashboard that merely puts these metrics together faces a readily available alternative. [Public product](https://www.coinglass.com/) |
| Token Metrics | Official documentation describes separate Trader and Investor Grades | Crypto asset ratings are also an existing category. I could not verify a readable current checkout price, so none is used here. [Official grade description](https://tokenmetrics.com/blog/introducing-token-metrics-investor-grade-in-2026/) |

My inference is that feature breadth or an AI label would be weak positioning for this repository. A more promising experiment is an unusually clear workflow for a particular analyst: changes across a bounded universe, traceable reasons, conflicting evidence, and a useful research history. This is a differentiation hypothesis, not a claim that competitors lack those features. Customers should compare the prototype with their actual existing routine.

There is no defensible proprietary advantage yet. Over time, advantages could come from a dependable point-in-time archive, an independently useful evaluation record, carefully maintained normalization, customer research histories, and a trusted distribution channel. Existing competitors can also build these. Customer preference and repeated use must demonstrate the advantage.

**Several design choices are worth preserving.** Separating observations, features, scores and explanations supports accountability. Immutable model versions and exact input lineage make historical questions answerable. Explicit missing-data and applicability rules prevent invented certainty. The two-stage scanner can contain expensive vendor and processing work. A modular monolith, persisted calculations, and a generated API client are sensible proposed boundaries for one team. These are design strengths; they have not yet been verified in software.

The following are the most consequential unresolved design and commercial issues. They are requirements to resolve during implementation, not observed defects in code that does not exist.

| Issue | Why it matters to users or revenue | Proposed treatment |
| --- | --- | --- |
| Technical slice versus paid value | A three-row ranking proves a pipeline but provides limited discovery and no retention loop | Keep it as the technical milestone; separately define the smallest paid workflow |
| Undefined research horizon | Intraday derivatives and slow fundamentals can produce an ambiguous “bullish” rank | Choose a primary research horizon and explain how slower context affects it |
| Cross-asset comparability | BTC has inapplicable fundamentals while ETH/SOL have applicable inputs; scores may summarize different evidence | Document the comparability policy; consider a common comparable core with explicit asset-specific overlays |
| Correlated inputs and regime treatment | Several momentum measures may repeatedly count one effect; regime may duplicate it again | Inspect feature redundancy and run simple ablations before adding complexity |
| Tiny normalization universe | Ranking within BTC/ETH/SOL can produce coarse or unstable relative positions | Treat three-asset normalization as a slice-specific method; validate the broader-universe method separately |
| Confidence terminology | Two directional confidence numbers can be mistaken for calibrated forecasts or complements | Test comprehension; distinguish bullish evidence, bearish evidence and data coverage in presentation |
| Historical knowledge versus event time | A backfilled or revised observation may be old by event date but unavailable when a signal was issued | Track availability/revision metadata where possible; distinguish original issued history from reconstructed history |
| Replay depth and retention | Stored feature snapshots allow score replay, but feature reconstruction needs retained input observations | Specify licensed retention for both levels and avoid promising reconstruction beyond stored evidence |
| Ranking publication consistency | Independently refreshed assets can make a table compare different times or model versions | Give ranking batches a snapshot identity and explicit readiness/partial-coverage rules |
| Job identity under model changes | A job key that only uses asset and time may block a legitimate new-version replay | Align calculation/model/input revision identities with database uniqueness and append-only semantics |
| Redis loss or duplicate workers | Disposable coordination must not compromise authoritative history | Enforce correctness and duplicate protection in PostgreSQL and test coordination loss |
| Business readiness | Auth, entitlements, billing, cancellation, support and operational recovery are not in the first slice | Specify them in a later paid-pilot execution plan before exposing a paid public service |

The fixed target of 15–25 features is an engineering acceptance requirement, not proof that customers need that many. The plan correctly requires a revision if fewer defensible inputs are available. Do not quietly change that scope. After the slice, expand only when a feature improves a tested workflow or evaluation result. Many of the ten eventual families bring additional licensing, historical coverage, and maintenance obligations.

**Correct calculations and useful signals need separate evidence.** Golden tests can prove that the same inputs yield the same score. They cannot prove that the score predicts returns or helps someone research effectively. I would track three independent forms of evidence:

1. Engineering correctness: identity, units, UTC, freshness, deterministic replay, duplicates, generated API compatibility, and failure handling.
2. Research usefulness: time to complete a real review, proportion of alerts judged relevant, evidence inspected, notes saved, repeat usage, and paid renewals.
3. Analytical usefulness: preregistered horizons and rules, timestamped original signals, complete outcome records, and comparisons against simple alternatives on unseen periods.

For the third category, start saving trustworthy issued history as soon as the pipeline works. Select one primary horizon for evaluation and label other horizons exploratory. Compare the rank with a simple relative-strength ordering and a broad eligible-universe baseline; disclose market exposure instead of treating positive returns in a rising market as sufficient evidence. Preserve losing and uninteresting cases, the universe as it existed, missing outcomes, and model changes. Overlapping forward-return windows are not independent observations. Use chronological evaluation, guard against revised data and repeated tuning on the same period, and document transaction assumptions if any later analysis claims executable strategy returns. This remains research measurement; it does not require implementing trading.

A useful, time-saving research tool can earn subscriptions before it demonstrates predictive performance. That commercial claim must be tested separately. If customers primarily buy because they expect an investment edge, time-saving evidence alone will not establish the benefit they are paying for.

**Data rights can determine whether the business is viable.** The repository already identifies licensing as a vendor-fit requirement. Current examples show why this belongs near the start of validation:

- CoinGlass labels Hobbyist and Startup as personal-use API plans. Its displayed Standard commercial-use offer is $299/month on annual billing, $3,588/year. Its general terms restrict redistribution and unauthorized commercial use. The exact paid application, archived history and customer-facing outputs still need matching rights. This price is not a complete approved data budget. [Pricing](https://www.coinglass.com/pricing), [terms](https://www.coinglass.com/terms).
- CoinGecko lists Basic at $35/month on monthly billing. Its commercial license permits charging for an application incorporating its data with attribution, while raw-data redistribution and white labeling require additional rights. A future API/export or publisher tier must be evaluated separately. [Pricing](https://www.coingecko.com/en/api/pricing), [license explanation](https://support.coingecko.com/hc/en-us/articles/16760512207257-What-Are-the-Differences-Between-Commercial-and-Custom-Licenses).
- DefiLlama's general terms restrict commercial exploitation and republishing without permission. A public endpoint or available dataset is not by itself authorization to incorporate it into a paid competing service. [Official terms](https://defillama.com/terms).

These are examples for commercial planning, not vendor selections or confirmations that the required ETH/SOL fundamentals and derivatives history are available under an acceptable contract. Before choosing providers, record the precise fields, units, covered assets, historical depth, cadences, retention, attribution, derived-output rights, display/export rights, and price for the proposed use. Buy the smallest licensed scope that supports the paid workflow. Avoid annual vendor commitments until their necessity and the demand signal are clearer.

If the business serves EU customers, personalized crypto recommendations deserve a specific legal scope review: MiCA's advice definition includes personalized recommendations relating to crypto transactions or crypto services. The absence of order execution does not by itself settle that question. Keep the intended service focused on general research and user-controlled information filters; have a qualified local adviser assess the actual proposed paid workflow, promotional claims, and jurisdiction before launch. This report does not determine regulatory classification or taxes. [ESMA's current Article 3 definitions](https://www.esma.europa.eu/publications-and-data/interactive-single-rulebook/mica/article-3-definitions).

**I would begin with one paid subscription.** Complexity in packaging will not compensate for an unproven daily habit. The following are prices to test, not measured willingness to pay. They are expressed excluding VAT for business modeling; customer-facing billing must show the applicable total.

| Offer | Proposed price | What the buyer receives | When to introduce it |
| --- | --- | --- | --- |
| Public sample | Free | A bounded or delayed brief and timestamped sample history, within data rights | To demonstrate the method and attract qualified conversations |
| Founding pilot | €29/month for a clearly disclosed two-month pilot | A narrow, usable brief and change-monitoring service with direct feedback | Once the service can actually be delivered, manually where appropriate |
| Core subscription | Test €49/month | A bounded qualified universe, watchlist changes, inspectable evidence, one alert channel and research history | After users demonstrate repeat value; validate renewal at the regular price |
| Small-team experiment | Test from €199/month | Defined seats, shared notes and history, and specific licensed reporting rights | Only after several buyers request and pay for a common team workflow |

The team offer should not silently include redistribution, unlimited audiences, raw feeds or white labeling. Price those only after rights and service costs are understood. A higher-priced concierge research service could bring earlier revenue, but would remain labor-intensive and would not prove a scalable subscription product.

I would defer advertising, broad free access, lifetime deals, and a public data API. They respectively depend on traffic, can create unfunded usage, conflict with recurring data costs, or introduce new licensing and reliability expectations. Sponsored asset ranking would undermine the product's reason to be trusted. If sponsorship is ever introduced, it must have no effect on analytical scores and must be visibly disclosed. Exchange referral income would also introduce a different incentive; subscriptions fit the stated research purpose more directly.

**Primary income requires retained customers and a cost model.** Your desired take-home income, existing audience, budget and available time were not supplied when this assessment was prepared. I therefore use illustrative euro scenarios rather than claiming a personal-income forecast.

The base model assumes a €49 monthly price excluding VAT, 15% of revenue reserved for payment costs, refunds and variable servicing, €1,000/month for data/hosting/overhead, and €500/month for customer acquisition. None of these cost assumptions is a vendor quote or an observed operating margin. Founder labor is compensated from the residual; additional staff, personal taxes, social contributions and reinvestment would reduce available income. Annual supplier prepayments can also require cash sooner than the monthly model suggests.

Monthly owner surplus before personal taxes = subscribers × net monthly price × (1 − variable-cost share) − fixed operating costs − acquisition budget.

| Target monthly owner surplus before personal taxes | Subscribers required at €49/month | Subscription revenue excluding VAT at that customer count |
| --- | ---: | ---: |
| €2,000 | 85 | €4,165 |
| €4,000 | 133 | €6,517 |
| €6,000 | 181 | €8,869 |

The counts round upward. They establish arithmetic requirements, not the likelihood or time needed to acquire those customers. A €4,000 take-home target would require more revenue than the €4,000 pre-tax case; no personal tax rate is assumed here.

| Net monthly price tested | Subscribers required for €4,000 pre-tax owner surplus under the same assumed costs |
| --- | ---: |
| €29 | 224 |
| €49 | 133 |
| €79 | 82 |
| €199 | 33 |

These rows do not imply that the same buyers will accept each price or that a team product has the same servicing costs. For example, 100 core customers at €49 plus 10 team accounts at €199 would produce €6,890 in monthly revenue and approximately €4,357 of pre-tax owner surplus under the base assumptions. That combination would need independent validation of both offers.

Costs matter: holding price and acquisition spend constant, the €4,000 target requires 121 subscribers with €500 fixed operating costs, 133 with €1,000, or 169 with €2,500. A vendor license decision can change how many customers the business must retain.

Churn also matters. In an illustrative 150-subscriber business, 5% monthly customer churn means losing an expected 7.5 subscribers per month; plan to replace roughly eight just to remain level. The same constant churn leaves approximately 54% of an original cohort after twelve months, without replacements. At 8%, approximately 37% remain. These are hypothetical sensitivities, not crypto SaaS benchmarks. An acquisition budget does not guarantee the replacements will arrive.

At the assumed €49 price and 15% variable costs, contribution before fixed costs is €41.65 per customer per month. An illustrative €100 cash acquisition cost takes about 2.4 paid months to recover before fixed costs and founder time; €250 takes about 6 months. Measure acquisition cost and retained cohorts before treating a lifetime-value formula as reliable.

For the decision to rely on this as primary income, I would require several consecutive months of actual owner-available cash after real costs, tax/social-contribution provisioning and a reinvestment reserve. I would also want evidence that retention and acquisition remain workable during quiet or adverse markets, plus an appropriate personal cash buffer. The size of that buffer depends on your obligations; this repository cannot establish it.

**The first customers should come from observing real work.** With no known existing audience, my recommended acquisition experiment is founder-led conversations followed by a narrow sample service. Interview about twenty people in the chosen segment; these counts are proposed experiment sizes, not market statistics. Ask them to reconstruct their last research session, show which tools they used, identify repeated checks, describe an alert they ignored, and explain their current spending. Ask what would have to happen for them to replace or add a subscription. Avoid relying on “would you use this?” responses.

Then observe users completing the same task with a sample brief and with their current routine. Measure time, evidence comprehension and whether they discover something relevant. The strongest early demand signal is a real payment followed by a voluntary renewal after using the delivered service. A waitlist, polite praise, or a large number of page views is weaker evidence.

Possible distribution channels to test are a recurring public research example, an email brief, participation in existing analyst communities under their rules, and small research-publisher collaborations with appropriate rights. Published examples should include timestamps, method versions, counterevidence, and later follow-ups on failures as well as successes. Public acquisition content can sit outside the dashboard; the proposed SPA does not yet provide a public publishing strategy. A full frontend framework change is unnecessary just to test a sample brief.

A useful experiment is to publish a repeatable series about meaningful changes and then revisit the original observations a week later. The call to action should be to try the matching watchlist workflow. Do not mass-message communities or fabricate testimonials. This assessment did not contact any potential customers or publish anything.

**I would run the next ninety days as validation stages.** The calendar is a suggested sequence, not an engineering delivery estimate. Available founder hours and provider approval can change it. The existing execution plan remains authoritative for implementation until explicitly revised.

| Window | Work to prioritize | Evidence that supports further investment |
| --- | --- | --- |
| Days 1–14 | Observe about 20 target-user workflows; define one task and primary horizon; make a sample; check candidate data rights and costs | Several users independently show the same repeated problem; at least five agree to a concrete paid pilot when delivered |
| Days 15–30 | Deliver a limited manual or partially automated pilot with permitted data; record real payments, use and objections; progress the technical slice within its plan | At least five actual paying users receive the promised service and can identify repeated value |
| Days 31–60 | Automate the recurring task; propose the next execution plan for explanations, saved watchlists, one notification channel, auth and billing; preserve issued history | Returning users, relevant notifications, usable explanations and first renewal evidence |
| Days 61–90 | Seek regular-price renewals, test one repeatable acquisition channel and calculate actual costs; assess expansion beyond BTC/ETH/SOL only with validated coverage | For example, 10 or more regular-price customers and renewal evidence from an eligible cohort; expanding usage without increasing support per customer |

Those are managerial decision thresholds, not statistically sufficient proof of product-market fit or investment performance. For a small pilot, I would treat 7 of 10 eligible customers renewing for a second paid month as encouraging directional evidence and inspect every cancellation. A three-person cohort cannot support a precise retention claim. Do not treat advance payments for an undelivered promise as product usage or retention.

If targeted users will not pay for a functioning, clearly differentiated brief after trying it, narrow or change the problem before adding the next indicator family. If paid users use it repeatedly but reject €49, investigate whether the issue is price, audience, incomplete value, or reliability. If users only want alerts and never inspect the large dashboard, simplify around that observed behavior. If commercial data terms exceed plausible revenue, narrow the data promise or negotiate a different licensed scope before expanding the build.

The first paid-product scope should be deliberately smaller than the full vision: a modest eligible universe sized to coverage and demand, meaningful changes, inspectable evidence and counterevidence, a saved watchlist, one notification channel, published timestamps/history, and basic customer operations. A candidate of roughly 20–50 liquid assets can be tested after the three-asset slice; it is a proposed scope, not a provider coverage claim. Historical outcome collection should begin early; advanced model comparison and probability calibration can follow later.

My recommendation is to preserve the repository's correctness foundations and reorganize the commercial milestones around observed customer value. The next business milestone is five people paying for and repeatedly using one research routine, followed by renewal at a sustainable price. The path from there to primary income depends on retention, distribution and licensed operating costs; the current documents establish none of those yet.

Assessment scope: all nine tracked documents reviewed; official competitor, pricing and licensing pages consulted; income calculations checked independently. No application implementation, vendor purchase, outreach, publishing, commit or push was performed. The report and its associated in-conversation calculator are analysis outputs. No application tests could be run because the repository has no application or test suite.
