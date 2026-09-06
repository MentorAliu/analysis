import type { RankingItem, RankingsResponse } from '@/lib/api/generated/types.gen'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Disclosure } from './rankings-feedback'
import { UtcTime } from './rankings-batch-context'
import { exactScore, scoreDirection } from '../format'

const categoryLabels = {
  price: 'Price',
  derivatives: 'Derivatives',
  fundamentals: 'Fundamentals',
  regime: 'Regime',
}
const categoryStates = {
  complete: 'Complete',
  partial: 'Partial',
  missing: 'Missing',
  inapplicable: 'Inapplicable',
}
export function RankingDetails({
  item,
  response,
  onClose,
}: {
  item: RankingItem
  response: RankingsResponse
  onClose: () => void
}) {
  return (
    <section
      id="ranking-detail-panel"
      aria-labelledby="ranking-detail-heading"
      className="ranking-panel flex flex-col gap-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex min-w-0 flex-col gap-2">
          <h2 id="ranking-detail-heading" className="ranking-heading" tabIndex={-1}>
            {item.symbol} details
          </h2>
          <p className="text-sm text-muted-foreground">
            {item.name} · {item.assetId}
          </p>
        </div>
        <Button type="button" size="touch" variant="outline" onClick={onClose}>
          Close and return to row
        </Button>
      </div>
      <p className="text-sm leading-6 text-muted-foreground">
        Model <span className="[overflow-wrap:anywhere]">{response.batch.model.id}</span> · As of{' '}
        <UtcTime value={response.batch.asOfUtc} />
      </p>
      <div className="flex flex-col gap-4">
        <h3 className="font-semibold">Exact scores</h3>
        <p className="text-sm text-muted-foreground">
          Original six-place values, in score points. Confidence scores are separate heuristics, not
          probabilities or complements.
        </p>
        <dl className="ranking-metadata">
          <div>
            <dt>Composite score (points)</dt>
            <dd className="tabular-nums" data-score-direction={scoreDirection(item.compositeScore)}>
              {item.compositeScore === null ? 'Not ready' : exactScore(item.compositeScore)}
            </dd>
          </div>
          <div>
            <dt>Bullish confidence score (points)</dt>
            <dd className="tabular-nums">{item.bullishConfidenceScore ?? 'Not ready'}</dd>
          </div>
          <div>
            <dt>Bearish confidence score (points)</dt>
            <dd className="tabular-nums">{item.bearishConfidenceScore ?? 'Not ready'}</dd>
          </div>
        </dl>
      </div>
      <div className="flex flex-col gap-4">
        <h3 className="font-semibold">Categories</h3>
        <div className="grid gap-6 sm:grid-cols-2">
          {item.categories.map((category) => (
            <section
              key={category.category}
              aria-label={`${categoryLabels[category.category]} category`}
              className="min-w-0 rounded-lg border p-4"
            >
              <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                <h4 className="font-medium">{categoryLabels[category.category]}</h4>
                <Badge variant={category.state === 'partial' ? 'attention' : 'outline'}>
                  {categoryStates[category.state]}
                </Badge>
              </div>
              {category.state === 'inapplicable' && (
                <p className="mb-4 text-sm leading-6 text-muted-foreground">
                  Not applicable to this asset in this model. Stored quality zero is a placeholder, not a
                  coverage assessment.
                </p>
              )}
              <dl className="ranking-metadata sm:grid-cols-1">
                <div>
                  <dt>Score (points)</dt>
                  <dd className="tabular-nums" data-score-direction={scoreDirection(category.score)}>
                    {category.state === 'inapplicable'
                      ? 'Not applicable'
                      : category.score === null
                        ? 'Missing — no category score'
                        : exactScore(category.score)}
                  </dd>
                </div>
                <div>
                  <dt>Category data quality (%)</dt>
                  <dd className="tabular-nums">
                    {category.state === 'inapplicable' ? 'Not applicable' : category.dataQualityPercent}
                  </dd>
                </div>
                <div>
                  <dt>Available / applicable weight numerators</dt>
                  <dd>
                    {category.availableWeightNumerator} / {category.applicableWeightNumerator}
                  </dd>
                </div>
                <div>
                  <dt>Model weight denominator</dt>
                  <dd>{response.batch.model.weightDenominator}</dd>
                </div>
              </dl>
            </section>
          ))}
        </div>
      </div>
      <div className="flex flex-col gap-4">
        <h3 className="font-semibold">Quality and coverage</h3>
        <dl className="ranking-metadata">
          <div>
            <dt>Data quality (%)</dt>
            <dd>{item.quality.dataQualityPercent}</dd>
          </div>
          <div>
            <dt>Context coverage (%)</dt>
            <dd>{item.quality.contextCoveragePercent}</dd>
          </div>
          <div>
            <dt>Core price readiness</dt>
            <dd>{item.quality.corePriceReady ? 'Ready' : 'Not ready'}</dd>
          </div>
          <div>
            <dt>Provider agreement</dt>
            <dd>Unassessed — single source</dd>
          </div>
        </dl>
        <p className="text-sm leading-6 text-muted-foreground">
          Data quality measures usable directional weight. Context coverage is separate. These assessments
          describe the original as-of and knowledge cutoff, not current provider health.
        </p>
      </div>
      <div className="flex flex-col gap-4">
        <h3 className="font-semibold">Feature states at calculation</h3>
        <dl className="ranking-metadata">
          {Object.entries(item.quality.featureStateCounts).map(([state, count]) => (
            <div key={state}>
              <dt className="capitalize">{state}</dt>
              <dd className="tabular-nums">{count}</dd>
            </div>
          ))}
        </dl>
      </div>
      <Disclosure title="Snapshot identifiers and hashes">
        <dl className="ranking-metadata">
          {(
            [
              ['Score snapshot ID', item.scoreSnapshotId],
              ['Feature snapshot ID', item.featureSnapshotId],
              ['Score hash', item.scoreHash],
              ['Feature hash', item.featureHash],
            ] as const
          ).map(([label, value]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd className="ranking-hash">{value}</dd>
            </div>
          ))}
        </dl>
      </Disclosure>
    </section>
  )
}
