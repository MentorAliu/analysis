import { createContext, useContext, useMemo } from 'react'
import { functionalUpdate, type SortingState } from '@tanstack/react-table'
import { DataTable } from '@/components/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import type { RankingItem, RankingsResponse } from '@/lib/api/generated/types.gen'
import type { DataTableColumn } from '@/lib/table'
import { compareDecimals, formatDecimal, scoreDirection } from '../format'
import { sortLabels, type RankingSort } from '../search'

const getRowId = (item: RankingItem) => item.assetId
const states = { complete: 'Complete', partial: 'Partial', 'not-ready': 'Not ready' }
const DetailsContext = createContext<{
  selectedAssetId: string | null
  onDetails: (assetId: string) => void
} | null>(null)
function DetailsButton({ item }: { item: RankingItem }) {
  const context = useContext(DetailsContext)!
  return (
    <Button
      id={`ranking-details-${item.assetId}`}
      type="button"
      size="touch"
      variant="ghost"
      aria-label={`View details for ${item.symbol}`}
      aria-expanded={context.selectedAssetId === item.assetId}
      aria-controls="ranking-detail-panel"
      onClick={() => context.onDetails(item.assetId)}
    >
      View details
    </Button>
  )
}

export function RankingsTable({
  items,
  sort,
  onSort,
  selectedAssetId,
  onDetails,
}: {
  items: RankingsResponse['items']
  sort: RankingSort
  onSort: (sort: RankingSort) => void
  selectedAssetId: string | null
  onDetails: (assetId: string) => void
}) {
  const sorting = useMemo<SortingState>(
    () =>
      sort === 'model'
        ? []
        : [{ id: sort.startsWith('composite') ? 'composite' : 'quality', desc: sort.endsWith('desc') }],
    [sort],
  )
  const columns = useMemo<DataTableColumn<RankingItem>[]>(
    () => [
      {
        accessorKey: 'rank',
        header: 'Model rank',
        enableSorting: false,
        meta: { align: 'right' },
        cell: ({ row }) => row.original.rank ?? 'Unranked',
      },
      {
        accessorKey: 'symbol',
        header: 'Asset',
        enableSorting: false,
        meta: { rowHeader: true, wrap: true },
        cell: ({ row }) => (
          <div className="flex min-w-24 flex-col items-start gap-2">
            <div>
              <span className="font-semibold">{row.original.symbol}</span>
              <span className="ml-2 font-normal text-muted-foreground">{row.original.name}</span>
            </div>
            <Badge variant={row.original.state === 'partial' ? 'attention' : 'outline'}>
              {states[row.original.state]}
            </Badge>
          </div>
        ),
      },
      {
        id: 'composite',
        accessorFn: (item) => item.compositeScore ?? undefined,
        header: 'Composite (points)',
        sortDescFirst: true,
        sortUndefined: 'last',
        sortFn: (a, b) => compareDecimals(a.original.compositeScore!, b.original.compositeScore!),
        meta: { align: 'right', wrap: true },
        cell: ({ row }) => (
          <span data-score-direction={scoreDirection(row.original.compositeScore)} className="font-medium">
            {row.original.compositeScore === null
              ? 'Not ready'
              : formatDecimal(row.original.compositeScore, true)}
          </span>
        ),
      },
      {
        id: 'quality',
        accessorFn: (item) => item.quality.dataQualityPercent,
        header: 'Data quality (%)',
        sortDescFirst: true,
        sortFn: (a, b) =>
          compareDecimals(a.original.quality.dataQualityPercent, b.original.quality.dataQualityPercent),
        meta: { align: 'right', wrap: true },
        cell: ({ row }) => formatDecimal(row.original.quality.dataQualityPercent),
      },
      {
        id: 'details',
        header: 'Details',
        enableSorting: false,
        cell: ({ row }) => <DetailsButton item={row.original} />,
      },
    ],
    [],
  )
  const detailContext = useMemo(() => ({ selectedAssetId, onDetails }), [selectedAssetId, onDetails])
  return (
    <section
      aria-labelledby="comparison-heading"
      className="flex min-w-0 flex-col gap-4"
      data-rankings-ready="true"
    >
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h2 className="ranking-heading" id="comparison-heading">
            Asset comparison
          </h2>
          <p className="text-sm text-muted-foreground">
            Display order: <span className="font-medium text-foreground">{sortLabels[sort]}</span>
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="touch"
          disabled={sort === 'model'}
          onClick={() => onSort('model')}
        >
          Return to model ranking
        </Button>
      </div>
      <p className="text-sm leading-6 text-muted-foreground">
        Composite and category scores are heuristic score points, not probabilities. Partial scores can rank;
        not-ready scores remain unranked.
      </p>
      {items.every((item) => item.rank === null) && (
        <p className="font-medium">No ranked scores in this stored batch.</p>
      )}
      <p id="table-scroll-help" className="text-sm text-muted-foreground">
        Scroll table horizontally when needed. Model rank always retains the API’s ranking.
      </p>
      <DetailsContext value={detailContext}>
        <DataTable
          data={items}
          columns={columns}
          getRowId={getRowId}
          caption="BTC, ETH and SOL stored ranking comparison"
          sorting={sorting}
          onSortingChange={(update) => {
            const next = functionalUpdate(update, sorting)[0]
            onSort(
              next
                ? `${next.id === 'composite' ? 'composite' : 'quality'}-${next.desc ? 'desc' : 'asc'}`
                : 'model',
            )
          }}
          density="comfortable"
          tableClassName="min-w-[38rem]"
          containerProps={{
            role: 'region',
            'aria-label': 'Ranking comparison table',
            'aria-describedby': 'table-scroll-help',
            tabIndex: 0,
            className: 'rounded-lg border bg-card p-1',
          }}
        />
      </DetailsContext>
    </section>
  )
}
