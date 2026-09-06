import { useCallback, useState, useSyncExternalStore } from 'react'
import { onlineManager, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { rankingsQueryOptions } from '../queries'
import { RankingsHttpError } from '../transport'
import { exactHour, utcLabel } from '../format'
import { selectionIdentity, type RankingSort, type RankingsSelection, type SelectionErrors } from '../search'
import { RankingsSelectionForm } from './rankings-selection-form'
import { RankingsBatchContext } from './rankings-batch-context'
import { RankingsFailure, RankingsLoading } from './rankings-feedback'
import { RankingsTable } from './rankings-table'
import { RankingDetails } from './ranking-details'

const subscribeOnline = (callback: () => void) => onlineManager.subscribe(callback)
const getOnline = () => onlineManager.isOnline()

export function RankingsPage({
  selection,
  sort,
  onSelection,
  onSort,
}: {
  selection: RankingsSelection
  sort: RankingSort
  onSelection: (selection: RankingsSelection) => void
  onSort: (sort: RankingSort) => void
}) {
  const client = useQueryClient()
  const options = rankingsQueryOptions(selection)
  const query = useQuery(options)
  const online = useSyncExternalStore(subscribeOnline, getOnline, () => true)
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null)
  const [cancelled, setCancelled] = useState(false)
  const [deniedDuringRetry, setDeniedDuringRetry] = useState(false)
  const [previousBatchId, setPreviousBatchId] = useState<string | null>(null)
  const busy = query.fetchStatus !== 'idle'
  const accessDenied =
    deniedDuringRetry || (query.error instanceof RankingsHttpError && query.error.problem.status === 403)
  const response = accessDenied ? undefined : query.data
  const selected = response?.items.find((item) => item.assetId === selectedAssetId)

  const refresh = async () => {
    if (busy) return
    setCancelled(false)
    setPreviousBatchId(query.data?.batch.id ?? null)
    if (accessDenied) setDeniedDuringRetry(true)
    const result = await query.refetch({ cancelRefetch: false })
    if (!result.error) setDeniedDuringRetry(false)
  }
  function submit(next: RankingsSelection) {
    if (selectionIdentity(next) === selectionIdentity(selection)) void refresh()
    else onSelection(next)
  }
  async function cancel() {
    await client.cancelQueries({ queryKey: options.queryKey, exact: true })
    setCancelled(true)
    document.getElementById(response ? 'rankings-refresh' : 'rankings-load')?.focus()
  }
  const openDetails = useCallback((assetId: string) => {
    setSelectedAssetId(assetId)
    requestAnimationFrame(() => document.getElementById('ranking-detail-heading')?.focus())
  }, [])
  function closeDetails() {
    const triggerId = `ranking-details-${selectedAssetId}`
    setSelectedAssetId(null)
    requestAnimationFrame(() => {
      const trigger = document.getElementById(triggerId)
      trigger?.focus()
      trigger?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
    })
  }
  let status = ''
  if (query.fetchStatus === 'paused')
    status = 'Request paused while offline. It will resume when the connection returns unless cancelled.'
  else if (busy)
    status = response ? 'Refreshing rankings. The displayed batch remains visible.' : 'Loading rankings.'
  else if (cancelled) status = 'Request cancelled.'
  else if (response && !query.error)
    status = `${previousBatchId === response.batch.id ? 'Refreshed; same stored batch.' : 'Loaded rankings.'} Model ${response.batch.model.id}, as of ${utcLabel(response.batch.asOfUtc)}. ${response.items.filter((item) => item.rank !== null).length} ranked assets.`

  return (
    <div className="rankings-page">
      <header className="flex flex-col gap-2">
        <h1 className="text-3xl font-semibold leading-9 tracking-tight">Rankings</h1>
        <p className="text-base leading-6 text-muted-foreground">Private research · BTC, ETH and SOL</p>
      </header>
      <RankingsSelectionForm
        selection={selection}
        onSubmit={submit}
        problem={query.error instanceof RankingsHttpError ? query.error.problem : undefined}
        busy={busy}
      />
      <div className="flex min-h-6 flex-wrap items-center gap-4">
        <p
          role="status"
          aria-live="polite"
          aria-atomic="true"
          className="text-sm leading-5 text-muted-foreground"
        >
          {status}
        </p>
        {busy && (
          <Button size="touch" type="button" variant="outline" onClick={() => void cancel()}>
            Cancel request
          </Button>
        )}
      </div>
      {!online && query.fetchStatus !== 'paused' && (
        <p className="text-sm text-muted-foreground">
          Offline. Displayed results retain their original retrieval time; refresh when you want another read.
        </p>
      )}
      {query.error && (
        <RankingsFailure
          error={query.error}
          retained={!!response}
          exact={!!selection.asOfUtc}
          onRetry={() => void refresh()}
          onDefault={() => onSelection({ modelId: 'slice1-v1' })}
          onLatest={() => onSelection({ modelId: selection.modelId })}
          busy={busy}
        />
      )}
      {!response && busy && <RankingsLoading />}
      {!response && cancelled && (
        <Button
          size="touch"
          type="button"
          variant="outline"
          className="self-start"
          onClick={() => void refresh()}
        >
          Retry request
        </Button>
      )}
      {response && (
        <>
          <RankingsBatchContext
            response={response}
            busy={busy}
            onRefresh={() => void refresh()}
            onExact={() =>
              onSelection({ modelId: response.batch.model.id, asOfUtc: exactHour(response.batch.asOfUtc) })
            }
          />
          <RankingsTable
            items={response.items}
            sort={sort}
            onSort={onSort}
            selectedAssetId={selectedAssetId}
            onDetails={openDetails}
          />
          {selected ? (
            <RankingDetails
              key={selected.assetId}
              item={selected}
              response={response}
              onClose={closeDetails}
            />
          ) : (
            <div id="ranking-detail-panel" hidden />
          )}
        </>
      )}
    </div>
  )
}

export function InvalidRankingsSelection({
  selection,
  errors,
  onSelection,
}: {
  selection: RankingsSelection
  errors: SelectionErrors
  onSelection: (selection: RankingsSelection) => void
}) {
  return (
    <div className="rankings-page">
      <h1 className="text-3xl font-semibold leading-9 tracking-tight">Rankings</h1>
      <RankingsSelectionForm selection={selection} initialErrors={errors} onSubmit={onSelection} />
      <Button
        variant="outline"
        size="touch"
        type="button"
        className="self-start"
        onClick={() => onSelection({ modelId: 'slice1-v1' })}
      >
        Use default selection
      </Button>
    </div>
  )
}
