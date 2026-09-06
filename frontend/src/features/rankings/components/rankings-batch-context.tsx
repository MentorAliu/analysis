import type { RankingsResponse } from '@/lib/api/generated/types.gen'
import { Button } from '@/components/ui/button'
import { ageLabel, utcLabel } from '../format'
import { Disclosure } from './rankings-feedback'

export function UtcTime({ value }: { value: string }) {
  return <time dateTime={value}>{utcLabel(value)}</time>
}

export function RankingsBatchContext({
  response,
  onRefresh,
  onExact,
  busy,
}: {
  response: RankingsResponse
  onRefresh: () => void
  onExact: () => void
  busy: boolean
}) {
  const { batch } = response
  return (
    <section aria-labelledby="batch-heading" className="flex min-w-0 flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex min-w-0 flex-col gap-2">
          <h2 className="ranking-heading" id="batch-heading">
            Displayed batch
          </h2>
          <p className="break-words text-sm text-muted-foreground">
            {response.selection === 'latest' ? 'Latest stored' : 'Exact historical hour'} · Model{' '}
            <span className="font-medium text-foreground [overflow-wrap:anywhere]">{batch.model.id}</span>
          </p>
        </div>
        <Button
          id="rankings-refresh"
          variant="outline"
          type="button"
          size="touch"
          aria-disabled={busy}
          onClick={onRefresh}
        >
          Refresh rankings
        </Button>
      </div>
      <dl className="ranking-metadata">
        <div>
          <dt>As of</dt>
          <dd>
            <UtcTime value={batch.asOfUtc} />
          </dd>
        </div>
        <div>
          <dt>Knowledge cutoff</dt>
          <dd>
            <UtcTime value={batch.knowledgeCutoffUtc} />
          </dd>
        </div>
        <div>
          <dt>Retrieved</dt>
          <dd>
            <UtcTime value={response.retrievedAtUtc} />
          </dd>
        </div>
      </dl>
      <div className="flex flex-col gap-2 border-l-2 border-primary pl-4">
        <h3 className="font-medium">Research reconstruction</h3>
        <p className="max-w-2xl text-sm leading-6 text-muted-foreground">
          Historical as-of does not mean this result existed at that time. It uses observations known by the
          stored knowledge cutoff, rather than representing an originally issued signal.
        </p>
      </div>
      <div className="flex flex-col items-start gap-2">
        <Disclosure title="Batch and model details">
          <dl className="ranking-metadata">
            <div>
              <dt>Created</dt>
              <dd>
                <UtcTime value={batch.createdAtUtc} />
              </dd>
            </div>
            <div>
              <dt>As-of age at retrieval</dt>
              <dd>
                {ageLabel(response.asOfAgeSeconds)} ({response.asOfAgeSeconds} seconds)
              </dd>
            </div>
            <div>
              <dt>Requested hour</dt>
              <dd>
                {response.requestedAsOfUtc ? (
                  <UtcTime value={response.requestedAsOfUtc} />
                ) : (
                  'Latest stored requested; no exact hour supplied'
                )}
              </dd>
            </div>
            <div>
              <dt>Selection</dt>
              <dd>{response.selection}</dd>
            </div>
            <div>
              <dt>Record kind</dt>
              <dd>{batch.recordKind}</dd>
            </div>
            <div>
              <dt>Universe asset IDs</dt>
              <dd>{batch.universeAssetIds.join(', ')}</dd>
            </div>
            <div>
              <dt>Batch ID</dt>
              <dd className="ranking-hash">{batch.id}</dd>
            </div>
            <div>
              <dt>Input hash</dt>
              <dd className="ranking-hash">{batch.inputHash}</dd>
            </div>
            {Object.entries(batch.model).map(([key, value]) => (
              <div key={key}>
                <dt>
                  {
                    (
                      {
                        id: 'Model ID',
                        manifestHash: 'Manifest hash',
                        calculatorSourceHash: 'Calculator source hash',
                        featureVersion: 'Feature version',
                        scorerVersion: 'Scorer version',
                        numericVersion: 'Numeric version',
                        status: 'Model status',
                        weightDenominator: 'Weight denominator',
                      } as Record<string, string>
                    )[key]
                  }
                </dt>
                <dd className="ranking-hash">{value}</dd>
              </div>
            ))}
          </dl>
        </Disclosure>
        {response.selection === 'latest' && (
          <Button variant="link" type="button" size="touch" onClick={onExact}>
            Use this exact hour
          </Button>
        )}
      </div>
    </section>
  )
}
